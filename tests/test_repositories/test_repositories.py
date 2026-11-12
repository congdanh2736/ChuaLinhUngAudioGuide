"""Test lớp Data Access: kiểm tra câu truy vấn SQL và ràng buộc khóa ngoại (không test nghiệp vụ ở đây)."""

from datetime import timedelta

import pytest

from app.core.exceptions import ConflictError
from app.core.security import utcnow
from tests.conftest import poi_data


async def make_poi(repos, code="P1", **kw):
    now = utcnow()
    return await repos["poi"].insert({**poi_data(code, **kw), "created_at": now, "updated_at": now})


class TestBaseRepository:
    async def test_insert_and_get_by_id(self, repos):
        poi = await make_poi(repos)
        assert poi.id
        found = await repos["poi"].get_by_id(poi.id)
        assert found.code == "P1"

    async def test_get_by_invalid_id_returns_none(self, repos):
        assert await repos["poi"].get_by_id("khong-phai-objectid") is None
        assert await repos["poi"].get_by_id("") is None

    async def test_update_returns_new_document(self, repos):
        poi = await make_poi(repos)
        updated = await repos["poi"].update(poi.id, {"name": "Tên mới"})
        assert updated.name == "Tên mới"

    async def test_update_missing_returns_none(self, repos):
        assert await repos["poi"].update("64b000000000000000000000", {"name": "x"}) is None

    async def test_delete(self, repos):
        poi = await make_poi(repos)
        assert await repos["poi"].delete(poi.id) is True
        assert await repos["poi"].delete(poi.id) is False

    async def test_unique_index_raises_conflict(self, repos):
        await make_poi(repos, "DUP")
        with pytest.raises(ConflictError):
            await make_poi(repos, "DUP")


class TestPoiRepository:
    async def test_list_active_sorted(self, repos):
        await make_poi(repos, "B", sort_order=2)
        await make_poi(repos, "A", sort_order=1)
        await make_poi(repos, "C", is_active=False)
        result = await repos["poi"].list_active()
        assert [p.code for p in result] == ["A", "B"]

    async def test_search_keyword_and_paging(self, repos):
        for i in range(5):
            await make_poi(repos, f"CODE{i}", name=f"Điểm {i}")
        items, total = await repos["poi"].search(keyword="CODE", is_active=None, skip=2, limit=2)
        assert total == 5
        assert len(items) == 2

    async def test_ids_exist_returns_missing(self, repos):
        poi = await make_poi(repos)
        missing = await repos["poi"].ids_exist([poi.id, "64b000000000000000000000", "sai"])
        assert missing == ["64b000000000000000000000", "sai"]


class TestLocalizationRepository:
    async def test_upsert_creates_then_updates(self, seeded_db, repos):
        p1 = await make_poi(repos, "P1")
        loc = await repos["localization"].upsert(p1.id, "en", {"name": "A", "status": "pending"})
        assert loc.name == "A" and loc.lang == "en"
        loc2 = await repos["localization"].upsert(p1.id, "en", {"status": "ready"})
        assert loc2.id == loc.id
        assert loc2.name == "A" and loc2.status == "ready"

    async def test_list_for_langs_filters(self, seeded_db, repos):
        p1, p2 = await make_poi(repos, "P1"), await make_poi(repos, "P2")
        await repos["localization"].upsert(p1.id, "en", {"status": "ready"})
        await repos["localization"].upsert(p1.id, "ja", {"status": "ready"})
        await repos["localization"].upsert(p2.id, "en", {"status": "ready"})
        result = await repos["localization"].list_for_langs(["en"], [p1.id])
        assert [(r.poi_id, r.lang) for r in result] == [(p1.id, "en")]

    async def test_count_by_status_and_delete_for_poi(self, seeded_db, repos):
        p1 = await make_poi(repos, "P1")
        await repos["localization"].upsert(p1.id, "en", {"status": "ready"})
        await repos["localization"].upsert(p1.id, "ja", {"status": "failed"})
        assert await repos["localization"].count_by_status() == {"ready": 1, "failed": 1}
        assert await repos["localization"].delete_for_poi(p1.id) == 2


async def make_tour(repos, poi_ids, code="T", **kw):
    now = utcnow()
    data = {
        "code": code,
        "name": "T",
        "poi_ids": poi_ids,
        "estimated_minutes": 10,
        "translations": {},
        "is_active": True,
        "created_at": now,
        "updated_at": now,
    }
    return await repos["tour"].insert({**data, **kw})


class TestTourRepository:
    async def test_remove_poi_everywhere(self, repos):
        a, b = await make_poi(repos, "A"), await make_poi(repos, "B")
        tour = await make_tour(repos, [a.id, b.id])
        assert await repos["tour"].remove_poi_everywhere(a.id) == 1
        assert (await repos["tour"].get_by_id(tour.id)).poi_ids == [b.id]

    async def test_poi_order_and_translations_round_trip(self, seeded_db, repos):
        a, b, c = [await make_poi(repos, code) for code in ("A", "B", "C")]
        tour = await make_tour(repos, [c.id, a.id, b.id], translations={"en": {"name": "Tour EN"}})
        assert tour.poi_ids == [c.id, a.id, b.id]  # giữ đúng thứ tự (cột position)
        assert tour.translations["en"].name == "Tour EN"
        updated = await repos["tour"].update(tour.id, {"poi_ids": [b.id], "translations": {}})
        assert updated.poi_ids == [b.id] and updated.translations == {}
        assert await repos["tour"].delete(tour.id) is True  # xóa luôn bảng con trong cùng giao dịch


class TestAccessSessionRepository:
    async def _session(self, repos, code, status, amount=50000):
        now = utcnow()
        return await repos["session"].insert(
            {
                "code": code,
                "method": "cash",
                "status": status,
                "amount": amount,
                "created_at": now,
                "code_expires_at": now + timedelta(hours=1),
            }
        )

    async def test_get_by_code_and_exists(self, repos):
        await self._session(repos, "ABC123", "paid")
        assert (await repos["session"].get_by_code("ABC123")).status == "paid"
        assert await repos["session"].code_exists("ABC123") is True
        assert await repos["session"].code_exists("ZZZ999") is False

    async def test_revenue_counts_only_paid_states(self, repos):
        await self._session(repos, "A1", "paid")
        await self._session(repos, "A2", "active")
        await self._session(repos, "A3", "pending")
        await self._session(repos, "A4", "cancelled")
        assert await repos["session"].total_revenue() == 100000


class TestEventRepository:
    async def test_top_pois(self, repos):
        now = utcnow()
        x, y = await make_poi(repos, "X"), await make_poi(repos, "Y")
        for poi_id, n in ((x.id, 3), (y.id, 1)):
            for _ in range(n):
                await repos["event"].insert({"poi_id": poi_id, "lang": "en", "type": "view", "created_at": now})
        top = await repos["event"].top_pois(5)
        assert top[0] == {"poi_id": x.id, "count": 3}
        assert await repos["event"].count_by_lang() == {"en": 4}


class TestLanguageRepository:
    async def test_upsert_keeps_admin_toggle(self, seeded_db, repos):
        await repos["language"].set_active("ja", False)
        await repos["language"].upsert(
            {
                "code": "ja",
                "name": "Japanese",
                "native_name": "日本語",
                "translator_code": "ja",
                "tts_voice": "v",
                "is_active": True,
            }
        )
        assert (await repos["language"].get_by_code("ja")).is_active is False
        assert len(await repos["language"].list_all(active_only=True)) == 15


class TestForeignKeys:
    """
    Ràng buộc khóa ngoại nằm Ở DATABASE (không chỉ ở code): kể cả xóa thẳng bằng câu lệnh SQL
    (giống thao tác trong SQL Server Management Studio) cũng bị từ chối.
    """

    async def test_cannot_delete_poi_that_has_localizations(self, seeded_db, repos):
        poi = await make_poi(repos)
        await repos["localization"].upsert(poi.id, "en", {"status": "ready"})
        with pytest.raises(ConflictError) as exc:
            await repos["poi"].delete(poi.id)
        assert exc.value.code == "REFERENCED_BY_OTHER_DATA"
        assert await repos["poi"].get_by_id(poi.id) is not None

    async def test_raw_sql_delete_is_blocked_by_database(self, seeded_db, repos):
        from sqlalchemy import text
        from sqlalchemy.exc import IntegrityError

        poi = await make_poi(repos)
        await repos["localization"].upsert(poi.id, "en", {"status": "ready"})
        with pytest.raises(IntegrityError):
            async with seeded_db.begin() as conn:
                await conn.execute(text("DELETE FROM pois WHERE id = :id"), {"id": poi.id})
        with pytest.raises(IntegrityError):  # ngôn ngữ đang có bản dịch cũng không xóa được
            async with seeded_db.begin() as conn:
                await conn.execute(text("DELETE FROM languages WHERE code = 'en'"))
        assert await repos["poi"].get_by_id(poi.id) is not None

    async def test_cannot_delete_poi_used_in_tour_or_visit_events(self, seeded_db, repos):
        in_tour, visited = await make_poi(repos, "IN_TOUR"), await make_poi(repos, "VISITED")
        await make_tour(repos, [in_tour.id])
        await repos["event"].insert({"poi_id": visited.id, "lang": "vi", "type": "view", "created_at": utcnow()})
        for poi in (in_tour, visited):
            with pytest.raises(ConflictError):
                await repos["poi"].delete(poi.id)

    async def test_child_must_reference_existing_parent(self, seeded_db, repos):
        poi = await make_poi(repos)
        with pytest.raises(ConflictError) as exc:
            await repos["localization"].upsert("khong-ton-tai", "en", {"status": "ready"})
        assert exc.value.code == "INVALID_REFERENCE"
        with pytest.raises(ConflictError):  # mã ngôn ngữ không có trong bảng languages
            await repos["localization"].upsert(poi.id, "xx", {"status": "ready"})
        with pytest.raises(ConflictError):
            await make_tour(repos, ["khong-ton-tai"])

    async def test_delete_allowed_after_children_removed(self, seeded_db, repos):
        poi = await make_poi(repos)
        await repos["localization"].upsert(poi.id, "en", {"status": "ready"})
        await repos["localization"].delete_for_poi(poi.id)
        assert await repos["poi"].delete(poi.id) is True
