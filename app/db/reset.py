"""
Xóa SẠCH database rồi tạo lại dữ liệu ban đầu (ngôn ngữ, admin, dữ liệu mẫu).
Dùng khi muốn demo lại từ đầu. Chạy:   python -m app.db.reset --yes

Xóa bằng DROP TABLE theo đúng thứ tự phụ thuộc (bảng con trước, bảng cha sau) rồi tạo lại,
vì khóa ngoại không cho xóa bảng cha khi bảng con còn dữ liệu.
"""

import asyncio
import sys

from app.core.config import get_settings
from app.db.database import mask_url, open_database
from app.db.seed import seed_languages, seed_sample_content
from app.repositories.repositories import AdminUserRepository
from app.services.auth_service import AuthService


async def reset() -> None:
    settings = get_settings()
    db = await open_database(settings.DATABASE_URL)
    try:
        await db.drop_all()
        await db.create_all()
        await seed_languages(db)
        await AuthService(user_repo=AdminUserRepository(db), settings=settings).ensure_first_admin(
            settings.FIRST_ADMIN_USERNAME, settings.FIRST_ADMIN_PASSWORD
        )
        if settings.SEED_SAMPLE_DATA:
            await seed_sample_content(db)
    finally:
        await db.dispose()
    print(f"Đã xóa và khởi tạo lại database tại {mask_url(settings.DATABASE_URL)}")


if __name__ == "__main__":
    if "--yes" not in sys.argv:
        print("Lệnh này XÓA TOÀN BỘ dữ liệu (POI, bản dịch, audio, mã truy cập...). Chạy lại với --yes để xác nhận.")
        sys.exit(1)
    asyncio.run(reset())
