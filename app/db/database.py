"""
Kết nối cơ sở dữ liệu SQL Server qua SQLAlchemy 2.0 (async).

- Mặc định: SQL Server qua driver ODBC, chuỗi kết nối dạng
    mssql+aioodbc://sa:MatKhau@localhost:1433/audio_guide?driver=ODBC+Driver+18+for+SQL+Server&TrustServerCertificate=yes
  hoặc đăng nhập bằng tài khoản Windows (SQL Server Express cài trên máy):
    mssql+aioodbc://@localhost\\SQLEXPRESS/audio_guide?driver=ODBC+Driver+18+for+SQL+Server&TrustServerCertificate=yes
  hoặc SQL Server LocalDB (cài kèm Visual Studio, không cần chạy service):
    mssql+aioodbc://@(localdb)\\MSSQLLocalDB/audio_guide?driver=ODBC+Driver+18+for+SQL+Server&TrustServerCertificate=yes
- "sqlite+aiosqlite://..." chỉ dành cho bộ test tự động (vẫn BẬT kiểm tra khóa ngoại: PRAGMA foreign_keys=ON).
"""

import logging

from sqlalchemy import event, text
from sqlalchemy.engine import make_url
from sqlalchemy.ext.asyncio import AsyncEngine, create_async_engine

from app.db.tables import metadata

logger = logging.getLogger(__name__)


class DatabaseUnavailableError(RuntimeError):
    """Không kết nối được cơ sở dữ liệu lúc khởi động."""


def mask_url(url: str) -> str:
    """Ẩn mật khẩu trong chuỗi kết nối khi ghi log: mssql+aioodbc://sa:***@host/db."""
    try:
        return make_url(url).render_as_string(hide_password=True)
    except Exception:  # noqa: BLE001 - chuỗi sai định dạng thì trả nguyên
        return url


class Database:
    """Bọc AsyncEngine; mọi Repository nhận đối tượng này (thay cho `db` của MongoDB trước đây)."""

    def __init__(self, url: str, *, echo: bool = False):
        self.url = url
        self.dialect = make_url(url).get_backend_name()
        options: dict = {"echo": echo, "pool_pre_ping": True}
        if self.dialect == "sqlite":
            options["connect_args"] = {"timeout": 30}
        self.engine: AsyncEngine = create_async_engine(url, **options)
        if self.dialect == "sqlite":
            # SQLite mặc định TẮT kiểm tra khóa ngoại -> bật lên để test giống SQL Server
            @event.listens_for(self.engine.sync_engine, "connect")
            def _enable_foreign_keys(dbapi_connection, _record):
                cursor = dbapi_connection.cursor()
                cursor.execute("PRAGMA foreign_keys=ON")
                cursor.close()

    @property
    def name(self) -> str:
        return make_url(self.url).database or ""

    def begin(self):
        """Mở 1 giao dịch: `async with db.begin() as conn:` - lỗi thì tự ROLLBACK, xong thì COMMIT."""
        return self.engine.begin()

    def connect(self):
        return self.engine.connect()

    async def create_all(self) -> None:
        """Code First: tạo bảng, khóa ngoại, chỉ mục còn thiếu (bảng đã có thì giữ nguyên dữ liệu)."""
        async with self.engine.begin() as conn:
            await conn.run_sync(metadata.create_all)

    async def drop_all(self) -> None:
        async with self.engine.begin() as conn:
            await conn.run_sync(metadata.drop_all)

    async def ping(self) -> bool:
        try:
            async with self.engine.connect() as conn:
                await conn.execute(text("SELECT 1"))
            return True
        except Exception:  # noqa: BLE001 - chỉ dùng cho health check
            return False

    async def dispose(self) -> None:
        await self.engine.dispose()


async def ensure_database_exists(url: str) -> None:
    """
    SQL Server: nếu database (vd audio_guide) chưa có thì tự tạo (cần quyền CREATE DATABASE).
    Người dùng chỉ cần cài SQL Server, không phải tự chạy lệnh CREATE DATABASE.
    """
    parsed = make_url(url)
    if parsed.get_backend_name() != "mssql" or not parsed.database:
        return
    name = parsed.database.replace("]", "]]")
    master = create_async_engine(parsed.set(database="master"), isolation_level="AUTOCOMMIT")
    try:
        async with master.connect() as conn:
            # READ_COMMITTED_SNAPSHOT: đọc không bị chặn bởi giao dịch ghi (tác vụ dịch chạy nền song song)
            await conn.execute(
                text(
                    f"IF DB_ID(N'{name}') IS NULL BEGIN "  # noqa: S608 - tên lấy từ cấu hình, đã escape ]
                    f"CREATE DATABASE [{name}]; ALTER DATABASE [{name}] SET READ_COMMITTED_SNAPSHOT ON; END"
                )
            )
    finally:
        await master.dispose()


async def open_database(url: str) -> Database:
    """Fail-fast: không kết nối được thì dừng ngay với hướng dẫn rõ ràng, thay vì lỗi khó hiểu lúc chạy."""
    try:
        await ensure_database_exists(url)
        db = Database(url)
        async with db.engine.connect() as conn:
            await conn.execute(text("SELECT 1"))
    except Exception as exc:  # noqa: BLE001
        raise DatabaseUnavailableError(
            f"Không kết nối được SQL Server tại {mask_url(url)} ({exc.__class__.__name__}: {exc}).\n"
            "  - Kiểm tra service 'SQL Server (MSSQLSERVER)' hoặc 'SQL Server (SQLEXPRESS)' đang chạy (services.msc)\n"
            "  - Lỗi 'Error Locating Server/Instance': sai tên server trong DATABASE_URL (máy chưa cài instance đó);\n"
            "    máy có Visual Studio thì dùng LocalDB: mssql+aioodbc://@(localdb)\\MSSQLLocalDB/audio_guide?...\n"
            "  - Cài 'ODBC Driver 18 for SQL Server' (hoặc 17) và ghi đúng tên driver trong DATABASE_URL\n"
            "  - Bật TCP/IP trong SQL Server Configuration Manager nếu dùng localhost:1433\n"
            "  - Hoặc chạy bằng Docker: docker compose up -d sqlserver"
        ) from None
    logger.info("Đã kết nối cơ sở dữ liệu: %s", mask_url(url))
    return db
