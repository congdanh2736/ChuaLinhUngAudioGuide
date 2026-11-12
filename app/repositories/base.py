"""
Repository cơ sở.

Repository là nơi DUY NHẤT được phép nói chuyện với cơ sở dữ liệu (SQL Server qua SQLAlchemy).
Nó nhận/trả về domain model (app/models/domain.py), giấu chi tiết như bảng, câu SQL, kiểu cột.

Để lớp Service KHÔNG phải sửa khi đổi từ MongoDB sang SQL Server, Repository giữ nguyên
giao diện cũ: bộ lọc vẫn viết dạng dict, vd {"is_active": True}, {"status": {"$in": [...]}},
{"created_at": {"$gte": t}} và được dịch sang mệnh đề WHERE tương ứng.

Vi phạm ràng buộc của database được đổi thành ConflictError (HTTP 409):
- trùng khóa UNIQUE                         -> code DUPLICATE
- xóa bản ghi đang bị khóa ngoại tham chiếu -> code REFERENCED_BY_OTHER_DATA
- tham chiếu tới bản ghi không tồn tại      -> code INVALID_REFERENCE
"""

import uuid
from typing import Any, Generic, TypeVar

from sqlalchemy import Table, and_, delete, func, insert, or_, select, true, update
from sqlalchemy.exc import IntegrityError
from sqlalchemy.ext.asyncio import AsyncConnection

from app.core.exceptions import ConflictError
from app.core.security import utcnow
from app.models.domain import DomainModel

ModelT = TypeVar("ModelT", bound=DomainModel)


def new_id() -> str:
    return uuid.uuid4().hex


def is_foreign_key_error(exc: IntegrityError) -> bool:
    message = str(exc.orig).lower()
    # SQLite: "FOREIGN KEY constraint failed"
    # SQL Server (lỗi 547): "... conflicted with the REFERENCE constraint / FOREIGN KEY constraint ..."
    return "foreign key" in message or "reference constraint" in message


def to_conflict(exc: IntegrityError, *, deleting: bool = False) -> ConflictError:
    if is_foreign_key_error(exc):
        if deleting:
            return ConflictError(
                "Không thể xóa vì dữ liệu này đang được dữ liệu khác tham chiếu (ràng buộc khóa ngoại)",
                code="REFERENCED_BY_OTHER_DATA",
            )
        return ConflictError(
            "Dữ liệu tham chiếu tới bản ghi không tồn tại (ràng buộc khóa ngoại)", code="INVALID_REFERENCE"
        )
    return ConflictError("Dữ liệu bị trùng (vi phạm khóa duy nhất)", code="DUPLICATE")


class BaseRepository(Generic[ModelT]):
    table: Table
    model: type[ModelT]

    def __init__(self, db):
        self.db = db

    @property
    def collection_name(self) -> str:  # tên cũ, giữ để tương thích
        return self.table.name

    # ---------- chuyển đổi bản ghi <-> model ----------
    def _row_to_dict(self, row) -> dict[str, Any]:
        # Bỏ cột NULL -> model dùng giá trị mặc định (giống document MongoDB thiếu trường)
        return {k: v for k, v in row._mapping.items() if v is not None}

    def _to_model(self, data: dict[str, Any] | None) -> ModelT | None:
        if data is None:
            return None
        return self.model.model_validate(data)

    def _columns(self, data: dict[str, Any]) -> dict[str, Any]:
        """Chỉ giữ các trường là cột của bảng (trường lạ bị bỏ qua, như model `extra="ignore"`)."""
        return {k: v for k, v in data.items() if k in self.table.c}

    # Hai "móc" để repository có bảng con (vd lộ trình -> tour_pois) mở rộng
    async def _attach_children(self, conn: AsyncConnection, items: list[dict[str, Any]]) -> None:
        return None

    async def _write_children(self, conn: AsyncConnection, item_id: str, data: dict[str, Any]) -> None:
        return None

    async def _load(self, conn: AsyncConnection, stmt) -> list[ModelT]:
        rows = [self._row_to_dict(r) for r in (await conn.execute(stmt)).all()]
        await self._attach_children(conn, rows)
        return [self._to_model(r) for r in rows]

    # ---------- dịch bộ lọc dạng dict -> WHERE ----------
    def _condition(self, field: str, value: Any):
        col = self.table.c[field]
        if isinstance(value, dict):
            parts = []
            for op, operand in value.items():
                if op == "$in":
                    parts.append(col.in_(list(operand)))
                elif op == "$nin":
                    parts.append(col.not_in(list(operand)))
                elif op == "$ne":
                    parts.append(col.is_not(None) if operand is None else col != operand)
                elif op == "$gt":
                    parts.append(col > operand)
                elif op == "$gte":
                    parts.append(col >= operand)
                elif op == "$lt":
                    parts.append(col < operand)
                elif op == "$lte":
                    parts.append(col <= operand)
                else:
                    raise ValueError(f"Toán tử lọc không hỗ trợ: {op}")
            return and_(*parts)
        return col.is_(None) if value is None else col == value

    def _where(self, filters: dict[str, Any] | None):
        parts = []
        for field, value in (filters or {}).items():
            if field == "$or":
                parts.append(or_(*[self._where(f) for f in value]))
            elif field == "id" or field in self.table.c:
                parts.append(self._condition(field, value))
            else:
                raise ValueError(f"Bảng {self.table.name} không có cột {field}")
        return and_(*parts) if parts else true()

    def _select(self, filters: dict[str, Any] | None = None):
        stmt = select(self.table)
        if filters:
            stmt = stmt.where(self._where(filters))
        return stmt

    # ---------- CRUD chung ----------
    async def get_by_id(self, item_id: str) -> ModelT | None:
        if not item_id:
            return None
        return await self.find_one({"id": item_id})

    async def find_one(self, filters: dict[str, Any]) -> ModelT | None:
        async with self.db.connect() as conn:
            items = await self._load(conn, self._select(filters).limit(1))
        return items[0] if items else None

    async def list(
        self,
        filters: dict[str, Any] | None = None,
        *,
        sort: list[tuple[str, int]] | None = None,
        skip: int = 0,
        limit: int = 0,
    ) -> list[ModelT]:
        stmt = self._select(filters)
        order = [self.table.c[f].desc() if d < 0 else self.table.c[f].asc() for f, d in (sort or [])]
        if skip or limit:
            order.append(self.table.c.id.asc())  # SQL Server bắt buộc ORDER BY khi phân trang (OFFSET)
        if order:
            stmt = stmt.order_by(*order)
        if skip:
            stmt = stmt.offset(skip)
        if limit:
            stmt = stmt.limit(limit)
        async with self.db.connect() as conn:
            return await self._load(conn, stmt)

    async def count(self, filters: dict[str, Any] | None = None) -> int:
        stmt = select(func.count()).select_from(self.table)
        if filters:
            stmt = stmt.where(self._where(filters))
        async with self.db.connect() as conn:
            return int((await conn.execute(stmt)).scalar_one())

    async def insert(self, data: dict[str, Any]) -> ModelT:
        item_id = data.get("id") or new_id()
        values = {**self._columns(data), "id": item_id}
        try:
            async with self.db.begin() as conn:
                await conn.execute(insert(self.table).values(**values))
                await self._write_children(conn, item_id, data)
        except IntegrityError as exc:
            raise to_conflict(exc) from exc
        return await self.get_by_id(item_id)

    async def update(self, item_id: str, data: dict[str, Any]) -> ModelT | None:
        if not item_id:
            return None
        values = self._columns(data)
        values.pop("id", None)
        try:
            async with self.db.begin() as conn:
                found = (await conn.execute(select(self.table.c.id).where(self.table.c.id == item_id))).first()
                if found is None:
                    return None
                if values:
                    await conn.execute(update(self.table).where(self.table.c.id == item_id).values(**values))
                await self._write_children(conn, item_id, data)
        except IntegrityError as exc:
            raise to_conflict(exc) from exc
        return await self.get_by_id(item_id)

    async def delete(self, item_id: str) -> bool:
        if not item_id:
            return False
        try:
            async with self.db.begin() as conn:
                await self._delete_children(conn, item_id)
                result = await conn.execute(delete(self.table).where(self.table.c.id == item_id))
        except IntegrityError as exc:
            raise to_conflict(exc, deleting=True) from exc
        return result.rowcount == 1

    async def _delete_children(self, conn: AsyncConnection, item_id: str) -> None:
        """Bảng con thuộc sở hữu của bản ghi (vd tour_pois của lộ trình) - xóa cùng giao dịch."""
        return None

    async def latest_update(self) -> Any:
        async with self.db.connect() as conn:
            return (await conn.execute(select(func.max(self.table.c.updated_at)))).scalar_one_or_none()

    @staticmethod
    def now():
        return utcnow()
