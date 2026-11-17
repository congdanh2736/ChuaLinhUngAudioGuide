/*
 * Thêm / sửa điểm tham quan.
 *   Thêm: POST /api/pois     -> PoiController.Create -> PoiService.CreateAsync (kiểm tra mã trùng, giờ chồng nhau)
 *                              -> PoiRepository.AddAsync -> LocalizationService.SchedulePoiAsync (xếp hàng dịch nền)
 *   Sửa : PUT  /api/pois/{id} -> PoiService.UpdateAsync (đổi tên / mô tả -> đánh dấu bản dịch cũ + dịch lại)
 * Dữ liệu sai (thiếu tên, tọa độ ngoài phạm vi...) -> FluentValidation trả 400, lỗi hiện dưới từng ô.
 */
import { initAdminLayout, setPageTitle } from "../layout.js";
import { poiApi, aiApi } from "../../../assets/js/api/poiApi.js";
import { toast } from "../../../assets/js/components/toast.js";
import { confirmDialog } from "../../../assets/js/components/modal.js";
import { CATEGORY_LABELS, AMENITY_LABELS, DAY_NAMES, DEFAULT_CENTER, MAP_TILE_URL } from "../../../assets/js/core/config.js";
import { escapeHtml, getIdFromUrl, setLoading } from "../../../assets/js/core/utils.js";

const $ = id => document.getElementById(id);
const form = $("poiForm");
const poiId = getIdFromUrl();
let map, marker, circle;

// [{day, open, close}] -> "06:00-11:30, 13:30-21:00" cho 1 ngày
function hoursToText(hours, day) {
    return (hours || []).filter(h => h.day === day).map(h => `${h.open}-${h.close}`).join(", ");
}

// Đọc ô nhập của 1 ngày. Trống = đóng cửa. Gõ sai -> báo lỗi rõ ràng.
function textToHours(text, day) {
    const result = [];
    for (const part of text.split(",").map(x => x.trim()).filter(Boolean)) {
        const m = part.match(/^(\d{1,2})[:h](\d{2})\s*-\s*(\d{1,2})[:h](\d{2})$/);
        if (!m) throw new Error(`${DAY_NAMES[day]}: "${part}" không đúng dạng, ví dụ 06:00-21:00`);
        const pad = n => String(n).padStart(2, "0");
        result.push({ day, open: `${pad(m[1])}:${m[2]}`, close: `${pad(m[3])}:${m[4]}` });
    }
    return result;
}

function fillForm(p) {
    form.code.value = p.code ?? "";
    form.name.value = p.name ?? "";
    form.description.value = p.description ?? "";
    form.category.value = p.category ?? "landmark";
    form.latitude.value = p.latitude;
    form.longitude.value = p.longitude;
    form.triggerRadiusM.value = p.triggerRadiusM ?? 30;
    form.priority.value = p.priority ?? 0;
    form.sortOrder.value = p.sortOrder ?? 0;
    form.thumbnailUrl.value = p.thumbnailUrl ?? "";
    form.entryFeeVnd.value = p.entryFeeVnd ?? 0;
    form.visitMinutes.value = p.visitMinutes ?? "";
    form.tips.value = p.tips ?? "";
    form.isActive.checked = p.isActive ?? true;
    $("hoursBody").innerHTML = DAY_NAMES.map((name, day) =>
        `<tr><td>${name}</td><td><input class="input hours-in" data-day="${day}" value="${escapeHtml(hoursToText(p.openingHours, day))}" placeholder="Trống = đóng cửa"></td></tr>`).join("");
    $("amenityGrid").innerHTML = Object.entries(AMENITY_LABELS).map(([code, label]) =>
        `<label class="checkbox"><input type="checkbox" class="amenity-in" value="${code}" ${(p.amenities || []).includes(code) ? "checked" : ""}> <span>${escapeHtml(label)}</span></label>`).join("");
}

// Bản đồ chọn tọa độ (Leaflet + OpenStreetMap)
function initMap(lat, lng, radius) {
    map = L.map("pickMap").setView([lat, lng], 18);
    L.tileLayer(MAP_TILE_URL, { maxZoom: 20, maxNativeZoom: 19, attribution: "© OpenStreetMap" }).addTo(map);
    marker = L.circleMarker([lat, lng], { radius: 7, color: "#fff", weight: 2, fillColor: "#9a3412", fillOpacity: 1 }).addTo(map);
    circle = L.circle([lat, lng], { radius, color: "#9a3412" }).addTo(map);
    map.on("click", e => {
        form.latitude.value = e.latlng.lat.toFixed(6);
        form.longitude.value = e.latlng.lng.toFixed(6);
        syncMap();
    });
    [form.latitude, form.longitude, form.triggerRadiusM].forEach(el => el.addEventListener("input", syncMap));
}

function syncMap() {
    const ll = [Number(form.latitude.value), Number(form.longitude.value)];
    if (Number.isNaN(ll[0]) || Number.isNaN(ll[1])) return;
    marker.setLatLng(ll);
    circle.setLatLng(ll);
    circle.setRadius(Number(form.triggerRadiusM.value) || 30);
}

function collect() {
    const num = el => (el.value === "" ? null : Number(el.value));
    return {
        code: form.code.value.trim(),
        name: form.name.value.trim(),
        description: form.description.value.trim(),
        category: form.category.value,
        latitude: num(form.latitude),
        longitude: num(form.longitude),
        triggerRadiusM: num(form.triggerRadiusM) ?? 30,
        priority: num(form.priority) ?? 0,
        sortOrder: num(form.sortOrder) ?? 0,
        thumbnailUrl: form.thumbnailUrl.value.trim(),
        entryFeeVnd: num(form.entryFeeVnd) ?? 0,
        visitMinutes: num(form.visitMinutes),
        tips: form.tips.value.trim(),
        isActive: form.isActive.checked,
        openingHours: [...document.querySelectorAll(".hours-in")].flatMap(el => textToHours(el.value, Number(el.dataset.day))),
        amenities: [...document.querySelectorAll(".amenity-in")].filter(el => el.checked).map(el => el.value)
    };
}

// Lỗi FluentValidation trả về {errors: {name: ["..."]}} -> hiện dưới đúng ô
function showErrors(err) {
    form.querySelectorAll(".form-error").forEach(el => (el.textContent = ""));
    form.querySelectorAll(".is-invalid").forEach(el => el.classList.remove("is-invalid"));
    const leftover = [];
    for (const [key, messages] of Object.entries(err.errors ?? {})) {
        const group = form.querySelector(`[data-field="${key.split(/[.[]/)[0]}"]`);
        if (group) {
            group.querySelector(".input, .select, .textarea")?.classList.add("is-invalid");
            group.querySelector(".form-error").textContent = [].concat(messages).join(" ");
        } else {
            leftover.push(...[].concat(messages));
        }
    }
    $("formError").textContent = leftover.length ? leftover.join("\n") : (err.errors ? "Vui lòng kiểm tra lại các ô được đánh dấu." : err.message);
    $("formError").scrollIntoView({ block: "nearest", behavior: "smooth" });
}

async function save(event) {
    event.preventDefault();
    $("formError").textContent = "";
    let body;
    try {
        body = collect();
    } catch (err) {
        $("formError").textContent = err.message;
        return;
    }
    setLoading($("btnSave"), true, "Đang lưu...");
    try {
        if (poiId) {
            await poiApi.update(poiId, body);
            toast.success("Đã lưu. Nếu đổi tên / nội dung, hệ thống đang dịch lại ở nền.");
        } else {
            const created = await poiApi.create(body);
            toast.success("Đã tạo điểm - đang dịch đa ngôn ngữ ở nền.");
            window.location.replace(`/admin/pois/localizations.html?id=${created.id}`);
            return;
        }
    } catch (err) {
        showErrors(err);
    } finally {
        setLoading($("btnSave"), false);
    }
}

async function enhanceWithAi(event) {
    const button = event.currentTarget;
    setLoading(button, true, "AI đang viết...");
    try {
        const r = await aiApi.enhanceDescription(form.name.value.trim(), form.description.value.trim());
        const ok = await confirmDialog({ title: "Gợi ý của AI", message: r.description + "\n\nDùng nội dung này?", confirmText: "Dùng" });
        if (ok) form.description.value = r.description;
    } catch (err) {
        toast.error(err.message);
    } finally {
        setLoading(button, false);
    }
}

if (await initAdminLayout({ title: poiId ? "Sửa điểm tham quan" : "Thêm điểm tham quan", adminOnly: true })) {
    form.category.innerHTML = Object.entries(CATEGORY_LABELS).map(([v, l]) => `<option value="${v}">${escapeHtml(l)}</option>`).join("");
    let poi = { latitude: DEFAULT_CENTER[0], longitude: DEFAULT_CENTER[1], triggerRadiusM: 30, priority: 0, isActive: true, openingHours: [], amenities: [] };
    if (poiId) {
        try {
            poi = await poiApi.getById(poiId);
            $("pageHeading").textContent = `Sửa: ${poi.name}`;
            setPageTitle(`Sửa: ${poi.name}`);
        } catch (err) {
            toast.error(err.message);
        }
    }
    fillForm(poi);
    initMap(poi.latitude, poi.longitude, poi.triggerRadiusM);
    $("btnCopyHours").addEventListener("click", () => {
        const inputs = [...document.querySelectorAll(".hours-in")];
        inputs.forEach(el => (el.value = inputs[0].value));
    });
    $("btnAi").addEventListener("click", enhanceWithAi);
    form.addEventListener("submit", save);
}
