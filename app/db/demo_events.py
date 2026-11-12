"""
Tạo lượt tham quan GIẢ LẬP để demo biểu đồ "Khung giờ đông khách" khi chưa có du khách thật.

    python -m app.db.demo_events            # tạo ~4 tuần dữ liệu cho mọi điểm đang hoạt động
    python -m app.db.demo_events --clear    # xóa toàn bộ dữ liệu giả lập (dữ liệu thật giữ nguyên)

Mọi sự kiện giả lập gắn với 1 phiên truy cập riêng có mã DEMO-SIMULATED (khóa ngoại session_id phải trỏ
tới phiên có thật) -> dễ phân biệt và xóa sạch, không lẫn với dữ liệu thật.
Mô phỏng: sáng sớm và chiều mát đông, trưa vắng, cuối tuần đông hơn ngày thường.
"""

import argparse
import asyncio
import random
from datetime import UTC, timedelta

from sqlalchemy import delete, insert, select

from app.core.config import get_settings
from app.core.security import utcnow
from app.db import tables as t
from app.db.database import open_database
from app.repositories.base import new_id
from app.services.poi_details import site_timezone

DEMO_CODE = "DEMO-SIMULATED"
# Mức đông tương đối theo giờ (giờ địa phương) - 0 là không có khách
HOURLY_WEIGHT = {
    5: 2, 6: 6, 7: 9, 8: 10, 9: 8, 10: 6, 11: 4, 12: 2, 13: 2,
    14: 3, 15: 6, 16: 9, 17: 10, 18: 7, 19: 4, 20: 2,
}  # fmt: skip


async def demo_session_id(db) -> str:
    """Phiên truy cập dùng chung cho mọi lượt giả lập (tạo nếu chưa có)."""
    async with db.begin() as conn:
        row = (await conn.execute(select(t.access_sessions.c.id).where(t.access_sessions.c.code == DEMO_CODE))).first()
        if row:
            return row.id
        session_id, now = new_id(), utcnow()
        await conn.execute(
            insert(t.access_sessions).values(
                id=session_id, code=DEMO_CODE, method="cash", status="cancelled", amount=0,
                note="Dữ liệu giả lập cho biểu đồ - không phải vé thật", created_at=now, code_expires_at=now,
            )
        )  # fmt: skip
        return session_id


async def generate(db, *, weeks: int = 4, seed: int = 7) -> int:
    rng = random.Random(seed)  # noqa: S311 - chỉ để giả lập dữ liệu, không dùng cho bảo mật
    tz = site_timezone(get_settings().SITE_UTC_OFFSET_HOURS)
    session_id = await demo_session_id(db)
    async with db.connect() as conn:
        pois = (await conn.execute(select(t.pois.c.id, t.pois.c.priority).where(t.pois.c.is_active.is_(True)))).all()
    today = utcnow().astimezone(tz).replace(hour=0, minute=0, second=0, microsecond=0)
    events = []
    for poi in pois:
        popularity = 0.5 + (poi.priority or 0) / 10  # điểm ưu tiên cao thì đông hơn
        for day_offset in range(1, weeks * 7 + 1):
            day = today - timedelta(days=day_offset)
            weekend = 1.6 if day.weekday() >= 5 else 1.0
            for hour, weight in HOURLY_WEIGHT.items():
                visits = rng.randint(0, round(weight * popularity * weekend))
                for _ in range(visits):
                    moment = day + timedelta(hours=hour, minutes=rng.randint(0, 59))
                    events.append(
                        {
                            "id": new_id(),
                            "poi_id": poi.id,
                            "session_id": session_id,
                            "lang": rng.choice(["vi", "vi", "en", "ko", "zh", "ja"]),
                            "type": rng.choice(["view", "audio_play", "geofence_enter"]),
                            "created_at": moment.astimezone(UTC),
                        }
                    )
    if events:
        async with db.begin() as conn:
            await conn.execute(insert(t.visit_events), events)
    return len(events)


async def clear(db) -> int:
    """Xóa lượt giả lập (bảng con) trước, rồi mới xóa phiên giả lập (bảng cha) - đúng thứ tự khóa ngoại."""
    async with db.begin() as conn:
        session_ids = select(t.access_sessions.c.id).where(t.access_sessions.c.code == DEMO_CODE)
        removed = await conn.execute(delete(t.visit_events).where(t.visit_events.c.session_id.in_(session_ids)))
        await conn.execute(delete(t.access_sessions).where(t.access_sessions.c.code == DEMO_CODE))
    return removed.rowcount


async def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--clear", action="store_true", help="Xóa dữ liệu giả lập")
    parser.add_argument("--weeks", type=int, default=4)
    args = parser.parse_args()

    settings = get_settings()
    db = await open_database(settings.DATABASE_URL)
    try:
        await db.create_all()
        removed = await clear(db)
        if args.clear:
            print(f"Đã xóa {removed} lượt tham quan giả lập.")
        else:
            created = await generate(db, weeks=args.weeks)
            print(f"Đã tạo {created} lượt tham quan giả lập ({args.weeks} tuần).")
            print("Xóa bằng: python -m app.db.demo_events --clear")
    finally:
        await db.dispose()


if __name__ == "__main__":
    asyncio.run(main())
