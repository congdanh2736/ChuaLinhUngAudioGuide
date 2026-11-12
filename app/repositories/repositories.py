"""
Các repository cụ thể - mỗi class ứng với 1 bảng (hoặc 1 bảng chính + các bảng con của nó).

Chỉ chứa câu truy vấn dữ liệu, KHÔNG chứa quy tắc nghiệp vụ
(vd: "mã hết hạn thì không cho đổi" là việc của Service, không phải ở đây).
Tên hàm và kiểu trả về giữ nguyên như bản MongoDB -> lớp Service không phải sửa.
"""

import hashlib
from datetime import datetime, timedelta
from typing import Any

from sqlalchemy import delete, func, insert, or_, select, update
from sqlalchemy.exc import IntegrityError
from sqlalchemy.ext.asyncio import AsyncConnection

from app.db import tables as t
from app.models.domain import (
    AccessSession,
    AdminUser,
    ChatLog,
    KnowledgeArticle,
    Language,
    Payment,
    Poi,
    PoiLocalization,
    Tour,
    VisitEvent,
)
from app.repositories.base import BaseRepository, new_id, to_conflict


def _like_pattern(keyword: str) -> str:
    """Tìm chuỗi con, coi %, _ và [ là ký tự thường (SQL Server hiểu [ ] là tập ký tự trong LIKE)."""
    escaped = keyword.lower().replace("\\", "\\\\").replace("%", "\\%").replace("_", "\\_").replace("[", "\\[")
    return f"%{escaped}%"


async def _group_count(db, column) -> dict[str, int]:
    stmt = select(column, func.count()).group_by(column)
    async with db.connect() as conn:
        return {key: int(n) for key, n in (await conn.execute(stmt)).all()}


class LanguageRepository(BaseRepository[Language]):
    table = t.languages
    model = Language

    async def get_by_code(self, code: str) -> Language | None:
        return await self.find_one({"code": code})

    async def list_all(self, *, active_only: bool = False) -> list[Language]:
        filters = {"is_active": True} if active_only else {}
        return await self.list(filters, sort=[("sort_order", 1)])

    async def set_active(self, code: str, is_active: bool) -> Language | None:
        async with self.db.begin() as conn:
            await conn.execute(update(self.table).where(self.table.c.code == code).values(is_active=is_active))
        return await self.get_by_code(code)

    async def upsert(self, data: dict[str, Any]) -> None:
        """Dùng khi seed: thêm nếu chưa có, không ghi đè trạng thái is_active admin đã chỉnh."""
        fields = {k: v for k, v in self._columns(data).items() if k not in ("is_active", "id")}
        async with self.db.begin() as conn:
            found = (await conn.execute(select(self.table.c.id).where(self.table.c.code == data["code"]))).first()
            if found:
                await conn.execute(update(self.table).where(self.table.c.id == found.id).values(**fields))
            else:
                await conn.execute(
                    insert(self.table).values(**fields, id=new_id(), is_active=data.get("is_active", True))
                )


class PoiRepository(BaseRepository[Poi]):
    table = t.pois
    model = Poi

    async def get_by_code(self, code: str) -> Poi | None:
        return await self.find_one({"code": code})

    async def list_active(self) -> list[Poi]:
        return await self.list({"is_active": True}, sort=[("sort_order", 1), ("name", 1)])

    async def search(self, *, keyword: str | None, is_active: bool | None, skip: int, limit: int):
        conditions = []
        if keyword:
            pattern = _like_pattern(keyword)
            conditions.append(
                or_(
                    func.lower(self.table.c.name).like(pattern, escape="\\"),
                    func.lower(self.table.c.code).like(pattern, escape="\\"),
                )
            )
        if is_active is not None:
            conditions.append(self.table.c.is_active == is_active)
        stmt = select(self.table).where(*conditions).order_by(self.table.c.sort_order, self.table.c.id)
        if skip:
            stmt = stmt.offset(skip)
        if limit:
            stmt = stmt.limit(limit)
        count_stmt = select(func.count()).select_from(self.table).where(*conditions)
        async with self.db.connect() as conn:
            items = await self._load(conn, stmt)
            total = int((await conn.execute(count_stmt)).scalar_one())
        return items, total

    async def ids_exist(self, ids: list[str]) -> list[str]:
        """Trả về các id KHÔNG tồn tại trong danh sách."""
        if not ids:
            return []
        async with self.db.connect() as conn:
            rows = await conn.execute(select(self.table.c.id).where(self.table.c.id.in_(list(set(ids)))))
            found = {r.id for r in rows}
        return [i for i in ids if i not in found]

    async def fill_details_if_missing(self, code: str, details: dict[str, Any]) -> bool:
        """Bổ sung thông tin chi tiết cho POI chưa có giờ mở cửa (opening_hours IS NULL). Trả về True nếu đã sửa."""
        stmt = (
            update(self.table)
            .where(self.table.c.code == code, self.table.c.opening_hours.is_(None))
            .values(**self._columns(details), updated_at=self.now())
        )
        async with self.db.begin() as conn:
            return (await conn.execute(stmt)).rowcount > 0


class PoiLocalizationRepository(BaseRepository[PoiLocalization]):
    table = t.poi_localizations
    model = PoiLocalization

    def _key(self, poi_id: str, lang: str):
        return (self.table.c.poi_id == poi_id) & (self.table.c.lang == lang)

    async def get(self, poi_id: str, lang: str) -> PoiLocalization | None:
        return await self.find_one({"poi_id": poi_id, "lang": lang})

    async def delete_one(self, poi_id: str, lang: str) -> bool:
        async with self.db.begin() as conn:
            return (await conn.execute(delete(self.table).where(self._key(poi_id, lang)))).rowcount == 1

    async def list_for_poi(self, poi_id: str) -> list[PoiLocalization]:
        return await self.list({"poi_id": poi_id})

    async def list_for_langs(self, langs: list[str], poi_ids: list[str] | None = None) -> list[PoiLocalization]:
        filters: dict[str, Any] = {"lang": {"$in": langs}}
        if poi_ids is not None:
            filters["poi_id"] = {"$in": poi_ids}
        return await self.list(filters)

    async def upsert(self, poi_id: str, lang: str, data: dict[str, Any]) -> PoiLocalization:
        fields = {k: v for k, v in self._columns(data).items() if k not in ("id", "poi_id", "lang")}
        fields["updated_at"] = self.now()
        try:
            async with self.db.begin() as conn:
                found = (await conn.execute(select(self.table.c.id).where(self._key(poi_id, lang)))).first()
                if found:
                    await conn.execute(update(self.table).where(self.table.c.id == found.id).values(**fields))
                else:
                    await conn.execute(insert(self.table).values(**fields, id=new_id(), poi_id=poi_id, lang=lang))
        except IntegrityError as exc:  # vd POI vừa bị xóa / mã ngôn ngữ không tồn tại
            raise to_conflict(exc) from exc
        return await self.get(poi_id, lang)

    async def mark_status_for_poi(self, poi_id: str, status: str, *, langs: list[str] | None = None) -> int:
        stmt = update(self.table).where(self.table.c.poi_id == poi_id)
        if langs is not None:
            stmt = stmt.where(self.table.c.lang.in_(langs))
        async with self.db.begin() as conn:
            return (await conn.execute(stmt.values(status=status, updated_at=self.now()))).rowcount

    async def list_by_status(self, statuses: list[str]) -> list[PoiLocalization]:
        return await self.list({"status": {"$in": statuses}})

    async def delete_for_poi(self, poi_id: str) -> int:
        async with self.db.begin() as conn:
            return (await conn.execute(delete(self.table).where(self.table.c.poi_id == poi_id))).rowcount

    async def count_by_status(self) -> dict[str, int]:
        return await _group_count(self.db, self.table.c.status)


class TourRepository(BaseRepository[Tour]):
    """Bảng chính `tours` + 2 bảng con: `tour_pois` (thứ tự POI) và `tour_translations` (bản dịch)."""

    table = t.tours
    model = Tour

    async def _attach_children(self, conn: AsyncConnection, items: list[dict[str, Any]]) -> None:
        if not items:
            return
        ids = [item["id"] for item in items]
        by_id = {item["id"]: item for item in items}
        for item in items:
            item["poi_ids"], item["translations"] = [], {}
        stops = await conn.execute(
            select(t.tour_pois).where(t.tour_pois.c.tour_id.in_(ids)).order_by(t.tour_pois.c.position)
        )
        for row in stops:
            by_id[row.tour_id]["poi_ids"].append(row.poi_id)
        translations = await conn.execute(select(t.tour_translations).where(t.tour_translations.c.tour_id.in_(ids)))
        for row in translations:
            by_id[row.tour_id]["translations"][row.lang] = {"name": row.name, "description": row.description}

    async def _write_children(self, conn: AsyncConnection, item_id: str, data: dict[str, Any]) -> None:
        if "poi_ids" in data:
            await conn.execute(delete(t.tour_pois).where(t.tour_pois.c.tour_id == item_id))
            rows = [{"tour_id": item_id, "poi_id": pid, "position": i} for i, pid in enumerate(data["poi_ids"] or [])]
            if rows:
                await conn.execute(insert(t.tour_pois), rows)
        if "translations" in data:
            await conn.execute(delete(t.tour_translations).where(t.tour_translations.c.tour_id == item_id))
            rows = []
            for lang, value in (data["translations"] or {}).items():
                value = value if isinstance(value, dict) else value.model_dump()
                rows.append(
                    {"tour_id": item_id, "lang": lang, "name": value["name"], "description": value.get("description")}
                )
            if rows:
                await conn.execute(insert(t.tour_translations), rows)

    async def _delete_children(self, conn: AsyncConnection, item_id: str) -> None:
        # Hai bảng con thuộc sở hữu của lộ trình -> xóa trong CÙNG giao dịch với lộ trình
        await conn.execute(delete(t.tour_pois).where(t.tour_pois.c.tour_id == item_id))
        await conn.execute(delete(t.tour_translations).where(t.tour_translations.c.tour_id == item_id))

    async def get_by_code(self, code: str) -> Tour | None:
        return await self.find_one({"code": code})

    async def list_active(self) -> list[Tour]:
        return await self.list({"is_active": True}, sort=[("name", 1)])

    async def mark_translation_requested(self, tour_id: str, lang: str) -> None:
        async with self.db.begin() as conn:
            current = (
                await conn.execute(select(self.table.c.translation_requested_at).where(self.table.c.id == tour_id))
            ).first()
            if current is None:
                return
            requested = dict(current.translation_requested_at or {})
            requested[lang] = self.now()
            await conn.execute(
                update(self.table).where(self.table.c.id == tour_id).values(translation_requested_at=requested)
            )

    async def remove_poi_everywhere(self, poi_id: str) -> int:
        async with self.db.begin() as conn:
            tour_ids = [
                r.tour_id
                for r in await conn.execute(
                    select(t.tour_pois.c.tour_id).where(t.tour_pois.c.poi_id == poi_id).distinct()
                )
            ]
            for tour_id in tour_ids:
                remaining = [
                    r.poi_id
                    for r in await conn.execute(
                        select(t.tour_pois.c.poi_id)
                        .where(t.tour_pois.c.tour_id == tour_id, t.tour_pois.c.poi_id != poi_id)
                        .order_by(t.tour_pois.c.position)
                    )
                ]
                await self._write_children(conn, tour_id, {"poi_ids": remaining})
                await conn.execute(update(self.table).where(self.table.c.id == tour_id).values(updated_at=self.now()))
        return len(tour_ids)


class AdminUserRepository(BaseRepository[AdminUser]):
    table = t.admin_users
    model = AdminUser

    async def get_by_username(self, username: str) -> AdminUser | None:
        return await self.find_one({"username": username})


class AccessSessionRepository(BaseRepository[AccessSession]):
    table = t.access_sessions
    model = AccessSession

    async def get_by_code(self, code: str) -> AccessSession | None:
        return await self.find_one({"code": code})

    async def code_exists(self, code: str) -> bool:
        return await self.count({"code": code}) > 0

    async def search(self, *, status: str | None, skip: int, limit: int):
        filters = {"status": status} if status else {}
        items = await self.list(filters, sort=[("created_at", -1)], skip=skip, limit=limit)
        return items, await self.count(filters)

    async def count_by_status(self) -> dict[str, int]:
        return await _group_count(self.db, self.table.c.status)

    async def total_revenue(self) -> int:
        stmt = select(func.coalesce(func.sum(self.table.c.amount), 0)).where(
            self.table.c.status.in_(["paid", "active", "expired"])
        )
        async with self.db.connect() as conn:
            return int((await conn.execute(stmt)).scalar_one())


class PaymentRepository(BaseRepository[Payment]):
    table = t.payments
    model = Payment


class KnowledgeRepository(BaseRepository[KnowledgeArticle]):
    table = t.knowledge_articles
    model = KnowledgeArticle

    async def list_active(self) -> list[KnowledgeArticle]:
        return await self.list({"is_active": True})


class ChatLogRepository(BaseRepository[ChatLog]):
    table = t.chat_logs
    model = ChatLog

    async def count_since(self, session_id: str, since: datetime) -> int:
        return await self.count({"session_id": session_id, "created_at": {"$gte": since}})

    async def recent_for_session(self, session_id: str, limit: int) -> list[ChatLog]:
        """Các lượt hỏi-đáp gần nhất của 1 phiên, xếp từ cũ đến mới."""
        items = await self.list({"session_id": session_id}, sort=[("created_at", -1)], limit=limit)
        return list(reversed(items))

    async def recent(self, limit: int = 20) -> list[ChatLog]:
        return await self.list({}, sort=[("created_at", -1)], limit=limit)


class VisitEventRepository(BaseRepository[VisitEvent]):
    table = t.visit_events
    model = VisitEvent

    async def top_pois(self, limit: int = 5) -> list[dict[str, Any]]:
        n = func.count().label("n")
        stmt = select(self.table.c.poi_id, n).group_by(self.table.c.poi_id).order_by(n.desc()).limit(limit)
        async with self.db.connect() as conn:
            return [{"poi_id": r.poi_id, "count": int(r.n)} for r in await conn.execute(stmt)]

    async def times_for_poi(self, poi_id: str, since: datetime) -> list[datetime]:
        """Thời điểm các lượt xem/nghe/đi qua 1 POI (chỉ lấy cột created_at cho nhẹ)."""
        stmt = select(self.table.c.created_at).where(self.table.c.poi_id == poi_id, self.table.c.created_at >= since)
        async with self.db.connect() as conn:
            return [r.created_at for r in await conn.execute(stmt)]

    async def count_by_lang(self) -> dict[str, int]:
        return await _group_count(self.db, self.table.c.lang)


class UiTranslationRepository:
    """Bản dịch chuỗi giao diện theo ngôn ngữ: {lang, strings: {key: text}, source_hash, updated_at}."""

    table = t.ui_translations

    def __init__(self, db):
        self.db = db

    async def get(self, lang: str) -> dict[str, Any] | None:
        async with self.db.connect() as conn:
            row = (await conn.execute(select(self.table).where(self.table.c.lang == lang))).first()
        return dict(row._mapping) if row else None

    async def save(self, lang: str, strings: dict[str, str], source_hash: str) -> None:
        values = {"strings": strings, "source_hash": source_hash, "updated_at": BaseRepository.now()}
        async with self.db.begin() as conn:
            result = await conn.execute(update(self.table).where(self.table.c.lang == lang).values(**values))
            if result.rowcount == 0:
                await conn.execute(insert(self.table).values(lang=lang, **values))


class ChatCacheRepository:
    """
    Cache câu trả lời chatbot: {key, answer, expires_at}.
    Bản ghi quá hạn bị bỏ qua khi đọc và được dọn mỗi lần ghi -> bảng không phình to, khởi động lại vẫn còn.
    """

    table = t.chat_cache

    def __init__(self, db):
        self.db = db

    @staticmethod
    def _hash(key: str) -> str:
        # Câu hỏi có thể rất dài -> lưu SHA-256 (64 ký tự) làm khóa chính
        return hashlib.sha256(key.encode()).hexdigest()

    async def get(self, key: str) -> dict[str, Any] | None:
        stmt = select(self.table.c.answer).where(
            self.table.c.key == self._hash(key), self.table.c.expires_at > BaseRepository.now()
        )
        async with self.db.connect() as conn:
            row = (await conn.execute(stmt)).first()
        return row.answer if row else None

    async def set(self, key: str, answer: dict[str, Any], ttl_seconds: int) -> None:
        now = BaseRepository.now()
        hashed = self._hash(key)
        values = {"answer": answer, "expires_at": now + timedelta(seconds=ttl_seconds)}
        async with self.db.begin() as conn:
            await conn.execute(delete(self.table).where(self.table.c.expires_at <= now))
            result = await conn.execute(update(self.table).where(self.table.c.key == hashed).values(**values))
            if result.rowcount == 0:
                await conn.execute(insert(self.table).values(key=hashed, **values))
