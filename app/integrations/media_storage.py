"""
Lưu file media (mp3) - thuộc lớp Data Access giống Repository nhưng cho file.

Hai cách lưu, cùng một "giao diện" (Service không cần biết đang dùng cách nào):
- DatabaseMediaStorage (mặc định, MEDIA_STORAGE=database): lưu bytes ngay trong SQL Server,
  bảng `media_files`, cột VARBINARY(MAX). Bền vững như dữ liệu khác: khởi động lại hay đổi máy đều còn.
  Mỗi file mp3 thuyết minh chỉ ~50–300 KB.
- LocalMediaStorage: lưu vào thư mục trên ổ đĩa (MEDIA_STORAGE=local).

Tên file = MD5(giọng + nội dung) -> cùng nội dung thì dùng lại, không tạo audio lần 2.
"""

import asyncio
import hashlib
from pathlib import Path

from sqlalchemy import func, insert, select, update

from app.core.security import utcnow
from app.db.tables import media_files

CONTENT_TYPES = {".mp3": "audio/mpeg", ".jpg": "image/jpeg", ".png": "image/png", ".webp": "image/webp"}


def guess_type(key: str) -> str:
    return CONTENT_TYPES.get(Path(key).suffix.lower(), "application/octet-stream")


class BaseMediaStorage:
    def __init__(self, url_prefix: str):
        self.url_prefix = url_prefix.rstrip("/")

    @staticmethod
    def audio_key(text: str, voice: str) -> str:
        return "audio/" + hashlib.md5(f"{voice}:{text}".encode()).hexdigest() + ".mp3"  # noqa: S324

    def url_for(self, key: str) -> str:
        return f"{self.url_prefix}/{key}"


class DatabaseMediaStorage(BaseMediaStorage):
    table = media_files

    def __init__(self, db, url_prefix: str):
        super().__init__(url_prefix)
        self.db = db

    async def exists(self, key: str) -> bool:
        stmt = select(func.count()).select_from(self.table).where(self.table.c.key == key)
        async with self.db.connect() as conn:
            return (await conn.execute(stmt)).scalar_one() > 0

    async def save(self, key: str, data: bytes, content_type: str | None = None) -> str:
        values = {
            "data": bytes(data),
            "content_type": content_type or guess_type(key),
            "size": len(data),
            "updated_at": utcnow(),
        }
        async with self.db.begin() as conn:
            result = await conn.execute(update(self.table).where(self.table.c.key == key).values(**values))
            if result.rowcount == 0:
                await conn.execute(insert(self.table).values(key=key, **values))
        return self.url_for(key)

    async def get(self, key: str) -> tuple[bytes, str] | None:
        stmt = select(self.table.c.data, self.table.c.content_type).where(self.table.c.key == key)
        async with self.db.connect() as conn:
            row = (await conn.execute(stmt)).first()
        if row is None:
            return None
        return bytes(row.data), row.content_type or guess_type(key)


class LocalMediaStorage(BaseMediaStorage):
    def __init__(self, root_dir: str, url_prefix: str):
        super().__init__(url_prefix)
        self.root = Path(root_dir)
        self.root.mkdir(parents=True, exist_ok=True)

    def _path(self, key: str) -> Path | None:
        path = (self.root / key).resolve()
        # Chặn path traversal (vd key = "../../etc/passwd")
        return path if path.is_relative_to(self.root.resolve()) else None

    async def exists(self, key: str) -> bool:
        path = self._path(key)
        return bool(path and path.exists() and path.stat().st_size > 0)

    async def save(self, key: str, data: bytes, content_type: str | None = None) -> str:
        path = self._path(key)
        if path is None:
            raise ValueError("Đường dẫn file không hợp lệ")
        path.parent.mkdir(parents=True, exist_ok=True)
        await asyncio.to_thread(path.write_bytes, data)
        return self.url_for(key)

    async def get(self, key: str) -> tuple[bytes, str] | None:
        path = self._path(key)
        if not path or not path.exists():
            return None
        return await asyncio.to_thread(path.read_bytes), guess_type(key)


def build_media_storage(settings, db) -> BaseMediaStorage:
    if settings.MEDIA_STORAGE == "local":
        return LocalMediaStorage(settings.MEDIA_DIR, settings.MEDIA_URL_PREFIX)
    return DatabaseMediaStorage(db, settings.MEDIA_URL_PREFIX)
