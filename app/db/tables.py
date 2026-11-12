"""
Lược đồ cơ sở dữ liệu SQL Server (Code First).

Các bảng được KHAI BÁO bằng code ở đây; lúc khởi động ứng dụng gọi `metadata.create_all()`
để SQL Server tạo bảng, khóa chính, khóa ngoại, ràng buộc UNIQUE và chỉ mục nếu chưa có.
Script SQL tương đương (để đọc / chạy tay trong SSMS): docs/database/schema-sqlserver.sql

QUY ƯỚC KHÓA NGOẠI: mọi khóa ngoại đều ON DELETE NO ACTION (mặc định của SQL Server).
=> Bản ghi "cha" đang được bản ghi "con" tham chiếu thì KHÔNG XÓA ĐƯỢC, kể cả khi xóa thẳng
   trong SQL Server Management Studio (SQL Server báo lỗi 547 - REFERENCE constraint).
   Vd: còn poi_localizations trỏ tới POI thì không xóa được POI; còn bản dịch / lộ trình dùng
   ngôn ngữ thì không xóa được ngôn ngữ.

Kiểu dữ liệu:
- Id: chuỗi 32 ký tự (uuid4 dạng hex) -> giữ nguyên kiểu `str` của domain model.
- Văn bản tiếng Việt / đa ngôn ngữ: NVARCHAR (Unicode).
- Thời gian: DATETIME2 lưu theo giờ UTC.
- Danh sách không phải tham chiếu (ảnh, tiện ích, giờ mở cửa, thẻ...): cột JSON (NVARCHAR(MAX)).
  Các danh sách có tham chiếu tới bảng khác (POI trong lộ trình) được tách thành bảng riêng có khóa ngoại.
"""

from datetime import UTC, datetime

from pydantic_core import to_jsonable_python
from sqlalchemy import (
    JSON,
    Boolean,
    Column,
    DateTime,
    Float,
    ForeignKey,
    Index,
    Integer,
    LargeBinary,
    MetaData,
    PrimaryKeyConstraint,
    String,
    Table,
    Unicode,
    UnicodeText,
    UniqueConstraint,
)
from sqlalchemy.dialects import mssql
from sqlalchemy.types import TypeDecorator

# Tên ràng buộc rõ ràng -> nhìn trong SSMS biết ngay ràng buộc nào bị vi phạm
NAMING_CONVENTION = {
    "pk": "pk_%(table_name)s",
    "fk": "fk_%(table_name)s_%(column_0_name)s",
    "uq": "uq_%(table_name)s_%(column_0_N_name)s",
    "ix": "ix_%(table_name)s_%(column_0_N_name)s",
    "ck": "ck_%(table_name)s_%(constraint_name)s",
}
metadata = MetaData(naming_convention=NAMING_CONVENTION)

# Văn bản dài / dữ liệu nhị phân: dùng kiểu hiện đại của SQL Server (không dùng NTEXT/IMAGE đã lỗi thời)
LongText = UnicodeText().with_variant(mssql.NVARCHAR("max"), "mssql")
Blob = LargeBinary().with_variant(mssql.VARBINARY("max"), "mssql")

ID_LEN = 32  # uuid4().hex
LANG_LEN = 16  # mã ngôn ngữ, vd "vi", "zh"
CODE_LEN = 64  # mã POI / lộ trình / mã truy cập
STATUS_LEN = 20


class UtcDateTime(TypeDecorator):
    """Lưu DATETIME2 (không kèm múi giờ) theo giờ UTC; đọc ra luôn là datetime có tzinfo=UTC."""

    impl = DateTime
    cache_ok = True

    def load_dialect_impl(self, dialect):
        if dialect.name == "mssql":
            return dialect.type_descriptor(mssql.DATETIME2(precision=6))
        return dialect.type_descriptor(DateTime())

    def process_bind_param(self, value, dialect):
        if value is None:
            return None
        if value.tzinfo is not None:
            value = value.astimezone(UTC).replace(tzinfo=None)
        return value

    def process_result_value(self, value, dialect):
        if value is None:
            return None
        if isinstance(value, str):  # phòng hờ driver trả về chuỗi
            value = datetime.fromisoformat(value)
        return value.replace(tzinfo=UTC) if value.tzinfo is None else value.astimezone(UTC)


class JsonValue(TypeDecorator):
    """Cột JSON (SQL Server: NVARCHAR(MAX)). Tự chuyển model Pydantic / datetime / enum sang JSON."""

    impl = JSON(none_as_null=True)
    cache_ok = True

    def process_bind_param(self, value, dialect):
        return None if value is None else to_jsonable_python(value)


def id_column() -> Column:
    return Column("id", String(ID_LEN), primary_key=True)


def fk(
    column: str,
    target: str,
    *,
    nullable: bool = False,
    length: int = ID_LEN,
    unicode: bool = False,
    index: bool = True,
) -> Column:
    """
    Cột khóa ngoại ON DELETE NO ACTION / ON UPDATE NO ACTION (không cho xóa cha khi còn con).
    SQL Server không tự tạo chỉ mục cho khóa ngoại -> tạo kèm (trừ khi cột đã đứng đầu khóa chính / UNIQUE).
    """
    col_type = Unicode(length) if unicode else String(length)
    return Column(column, col_type, ForeignKey(target), nullable=nullable, index=index)


# ============================================================ Danh mục
languages = Table(
    "languages",
    metadata,
    id_column(),
    Column("code", Unicode(LANG_LEN), nullable=False, unique=True),
    Column("name", Unicode(100), nullable=False),
    Column("native_name", Unicode(100), nullable=False),
    Column("translator_code", Unicode(LANG_LEN), nullable=False),
    Column("tts_voice", Unicode(100), nullable=False),
    Column("is_active", Boolean, nullable=False, default=True),
    Column("sort_order", Integer, nullable=False, default=0),
)

pois = Table(
    "pois",
    metadata,
    id_column(),
    Column("code", Unicode(CODE_LEN), nullable=False, unique=True),
    Column("name", Unicode(255), nullable=False),
    Column("description", LongText, nullable=False),
    Column("category", Unicode(50), nullable=False, default="landmark"),
    Column("latitude", Float, nullable=False),
    Column("longitude", Float, nullable=False),
    Column("trigger_radius_m", Integer, nullable=False, default=30),
    Column("priority", Integer, nullable=False, default=0),
    Column("thumbnail_url", Unicode(1000)),
    Column("image_urls", JsonValue, nullable=False, default=list),
    Column("sort_order", Integer, nullable=False, default=0),
    Column("is_active", Boolean, nullable=False, default=True),
    Column("opening_hours", JsonValue),  # NULL = POI cũ chưa có thông tin chi tiết (xem seed.backfill)
    Column("entry_fee_vnd", Integer, nullable=False, default=0),
    Column("visit_minutes", Integer),
    Column("amenities", JsonValue, nullable=False, default=list),
    Column("tips", LongText, nullable=False, default=""),
    Column("created_at", UtcDateTime, nullable=False),
    Column("updated_at", UtcDateTime, nullable=False),
    Index(None, "is_active", "sort_order"),
)

poi_localizations = Table(
    "poi_localizations",
    metadata,
    id_column(),
    fk("poi_id", "pois.id", index=False),
    fk("lang", "languages.code", length=LANG_LEN, unicode=True),
    Column("name", Unicode(255)),
    Column("description", LongText),
    Column("audio_url", Unicode(500)),  # trỏ tới media_files.key (là URL, không phải khóa ngoại)
    Column("status", String(STATUS_LEN), nullable=False, default="pending"),
    Column("translated_by", String(STATUS_LEN), nullable=False, default="machine"),
    Column("source_hash", String(64)),
    Column("tips", LongText),
    Column("tips_hash", String(64)),
    Column("error", LongText),
    Column("updated_at", UtcDateTime, nullable=False),
    UniqueConstraint("poi_id", "lang"),  # mỗi POI chỉ có 1 bản dịch cho mỗi ngôn ngữ
    Index(None, "status"),
)

tours = Table(
    "tours",
    metadata,
    id_column(),
    Column("code", Unicode(CODE_LEN), nullable=False, unique=True),
    Column("name", Unicode(255), nullable=False),
    Column("description", LongText),
    Column("estimated_minutes", Integer, nullable=False, default=30),
    Column("translation_requested_at", JsonValue, nullable=False, default=dict),  # {lang: thời điểm yêu cầu dịch}
    Column("is_active", Boolean, nullable=False, default=True),
    Column("created_at", UtcDateTime, nullable=False),
    Column("updated_at", UtcDateTime, nullable=False),
)

# Bảng trung gian N-N: lộ trình gồm nhiều POI theo thứ tự (thay cho mảng poi_ids của MongoDB)
tour_pois = Table(
    "tour_pois",
    metadata,
    fk("tour_id", "tours.id", index=False),
    fk("poi_id", "pois.id"),
    Column("position", Integer, nullable=False),
    PrimaryKeyConstraint("tour_id", "position"),
)

# Bản dịch tên / mô tả lộ trình theo ngôn ngữ
tour_translations = Table(
    "tour_translations",
    metadata,
    fk("tour_id", "tours.id", index=False),
    fk("lang", "languages.code", length=LANG_LEN, unicode=True),
    Column("name", Unicode(255), nullable=False),
    Column("description", LongText),
    PrimaryKeyConstraint("tour_id", "lang"),
)

# ============================================================ Người dùng, truy cập, thanh toán
admin_users = Table(
    "admin_users",
    metadata,
    id_column(),
    Column("username", Unicode(100), nullable=False, unique=True),
    Column("password_hash", String(255), nullable=False),
    Column("full_name", Unicode(255), nullable=False),
    Column("role", String(STATUS_LEN), nullable=False, default="staff"),
    Column("is_active", Boolean, nullable=False, default=True),
    Column("created_at", UtcDateTime, nullable=False),
    Column("last_login_at", UtcDateTime),
)

access_sessions = Table(
    "access_sessions",
    metadata,
    id_column(),
    Column("code", Unicode(CODE_LEN), nullable=False, unique=True),
    Column("method", String(STATUS_LEN), nullable=False),
    Column("status", String(STATUS_LEN), nullable=False),
    Column("amount", Integer, nullable=False),
    Column("currency", String(8), nullable=False, default="VND"),
    fk("created_by", "admin_users.id", nullable=True),  # nhân viên bán mã (tiền mặt); NULL nếu mua online
    Column("note", Unicode(500)),
    Column("created_at", UtcDateTime, nullable=False),
    Column("code_expires_at", UtcDateTime, nullable=False),
    Column("paid_at", UtcDateTime),
    Column("activated_at", UtcDateTime),
    Column("access_expires_at", UtcDateTime),
    Index(None, "status", "created_at"),
)

payments = Table(
    "payments",
    metadata,
    id_column(),
    fk("session_id", "access_sessions.id"),
    Column("provider", Unicode(50), nullable=False),
    Column("amount", Integer, nullable=False),
    Column("currency", String(8), nullable=False, default="VND"),
    Column("status", String(STATUS_LEN), nullable=False, default="pending"),
    Column("provider_ref", Unicode(255)),
    Column("created_at", UtcDateTime, nullable=False),
    Column("completed_at", UtcDateTime),
)

# ============================================================ Chatbot, thống kê
knowledge_articles = Table(
    "knowledge_articles",
    metadata,
    id_column(),
    Column("title", Unicode(255), nullable=False),
    Column("content", LongText, nullable=False),
    Column("tags", JsonValue, nullable=False, default=list),
    Column("is_active", Boolean, nullable=False, default=True),
    Column("created_at", UtcDateTime, nullable=False),
    Column("updated_at", UtcDateTime, nullable=False),
)

chat_logs = Table(
    "chat_logs",
    metadata,
    id_column(),
    fk("session_id", "access_sessions.id", nullable=True, index=False),  # NULL = admin xem thử / không cần mã
    fk("lang", "languages.code", length=LANG_LEN, unicode=True),
    Column("question", LongText, nullable=False),
    Column("answer", LongText, nullable=False),
    Column("source_ids", JsonValue, nullable=False, default=list),  # id POI / bài viết đã dùng (chỉ để tham khảo)
    Column("used_llm", Boolean, nullable=False, default=False),
    Column("created_at", UtcDateTime, nullable=False),
    Index(None, "session_id", "created_at"),
)

visit_events = Table(
    "visit_events",
    metadata,
    id_column(),
    fk("session_id", "access_sessions.id", nullable=True),
    fk("poi_id", "pois.id"),
    Column("lang", Unicode(LANG_LEN), nullable=False),
    Column("type", String(STATUS_LEN), nullable=False),
    Column("created_at", UtcDateTime, nullable=False),
    Index(None, "poi_id", "created_at"),
)

# ============================================================ Bảng kỹ thuật (không có khóa ngoại)
ui_translations = Table(
    "ui_translations",
    metadata,
    fk("lang", "languages.code", length=LANG_LEN, unicode=True, index=False),
    Column("strings", JsonValue, nullable=False),
    Column("source_hash", String(64), nullable=False),
    Column("updated_at", UtcDateTime, nullable=False),
    PrimaryKeyConstraint("lang"),
)

chat_cache = Table(
    "chat_cache",
    metadata,
    Column("key", String(64), primary_key=True),  # SHA-256 của (ngôn ngữ + câu hỏi đã chuẩn hóa)
    Column("answer", JsonValue, nullable=False),
    Column("expires_at", UtcDateTime, nullable=False, index=True),
)

media_files = Table(
    "media_files",
    metadata,
    Column("key", Unicode(255), primary_key=True),  # vd "audio/<md5>.mp3"
    Column("data", Blob, nullable=False),  # SQL Server: VARBINARY(MAX)
    Column("content_type", String(100), nullable=False),
    Column("size", Integer, nullable=False),
    Column("updated_at", UtcDateTime, nullable=False),
)
