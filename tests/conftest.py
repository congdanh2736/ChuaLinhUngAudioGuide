"""
Fixture dùng chung cho test.

Mặc định dùng SQLite (file tạm, BẬT kiểm tra khóa ngoại) -> chạy ở đâu cũng được, không cần cài SQL Server.
Nếu đặt biến môi trường TEST_DATABASE_URL trỏ tới SQL Server (CI job "integration" làm vậy)
thì toàn bộ test repository / service chạy trên SQL Server THẬT.
"""

import os

import pytest

from app.core.config import Settings
from app.db.database import Database, ensure_database_exists
from app.db.seed import seed_languages
from app.integrations.media_storage import LocalMediaStorage
from app.repositories.repositories import (
    AccessSessionRepository,
    AdminUserRepository,
    ChatCacheRepository,
    ChatLogRepository,
    KnowledgeRepository,
    LanguageRepository,
    PaymentRepository,
    PoiLocalizationRepository,
    PoiRepository,
    TourRepository,
    VisitEventRepository,
)
from app.services.task_runner import InlineTaskRunner
from tests.fakes import FakeTranslator, FakeTTS


@pytest.fixture
def settings(tmp_path) -> Settings:
    return Settings(
        _env_file=None,
        APP_ENV="test",
        DATABASE_URL=f"sqlite+aiosqlite:///{tmp_path / 'app.db'}",
        JWT_SECRET="test-secret-key-that-is-long-enough-123",
        TRANSLATOR_PROVIDER="none",
        TTS_PROVIDER="none",
        LLM_PROVIDER="none",
        MEDIA_DIR=str(tmp_path / "media"),
        SEED_SAMPLE_DATA=False,
        CHAT_DAILY_LIMIT=5,
    )


@pytest.fixture
async def db(tmp_path):
    real_url = os.getenv("TEST_DATABASE_URL")
    if real_url:
        await ensure_database_exists(real_url)
        database = Database(real_url)
        await database.drop_all()  # mỗi test bắt đầu với các bảng rỗng
    else:
        database = Database(f"sqlite+aiosqlite:///{tmp_path / 'test.db'}")
    await database.create_all()
    yield database
    if real_url:
        await database.drop_all()
    await database.dispose()


@pytest.fixture
async def seeded_db(db):
    await seed_languages(db)
    return db


@pytest.fixture
def repos(db) -> dict:
    return {
        "language": LanguageRepository(db),
        "poi": PoiRepository(db),
        "localization": PoiLocalizationRepository(db),
        "tour": TourRepository(db),
        "user": AdminUserRepository(db),
        "session": AccessSessionRepository(db),
        "payment": PaymentRepository(db),
        "knowledge": KnowledgeRepository(db),
        "chat": ChatLogRepository(db),
        "event": VisitEventRepository(db),
        "chat_cache": ChatCacheRepository(db),
    }


@pytest.fixture
def translator():
    return FakeTranslator()


@pytest.fixture
def tts():
    return FakeTTS()


@pytest.fixture
def storage(tmp_path):
    return LocalMediaStorage(str(tmp_path / "media"), "/media")


@pytest.fixture
def task_runner():
    return InlineTaskRunner()


def poi_data(code: str = "TEST_POI", **overrides) -> dict:
    data = {
        "code": code,
        "name": "Tượng Phật Bà Quan Thế Âm",
        "description": "Tượng cao khoảng 67 mét, hướng ra biển. Người dân tin Ngài che chở ngư dân.",
        "category": "statue",
        "latitude": 16.1003,
        "longitude": 108.2779,
        "trigger_radius_m": 30,
        "priority": 5,
        "image_urls": [],
        "sort_order": 0,
        "is_active": True,
    }
    data.update(overrides)
    return data
