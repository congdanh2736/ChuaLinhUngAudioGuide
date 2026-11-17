"""
Trang CỔNG THANH TOÁN GIẢ LẬP (thay Payoo khi demo).

Trang này đóng vai "bên thứ 3": khi khách bấm Thanh toán, nó tự ký payload rồi gọi
PaymentService.handle_webhook - đúng như cổng thật gọi webhook về backend -
sau đó chuyển khách về app kèm mã truy cập.
Chỉ bật khi PAYMENT_PROVIDER=mock và ẩn khỏi Swagger.
"""

import html
import uuid
from urllib.parse import quote

from fastapi import APIRouter, Depends, Form
from fastapi.responses import HTMLResponse, RedirectResponse

from app.api.deps import get_payment_service, get_state
from app.core.exceptions import AppError

router = APIRouter(prefix="/mock-gateway", include_in_schema=False)

PAGE = """<!doctype html><html lang="vi"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1"><title>Cổng thanh toán (giả lập)</title>
<style>body{{font-family:Arial,sans-serif;margin:0;padding:16px;color:#222}}
.box{{max-width:360px;margin:20px auto;border:1px solid #aaa;padding:14px 16px}}
.warn{{background:#fff3c4;border:1px solid #d8bd55;padding:4px 6px;font-size:12px}}
.amount{{font-size:26px;font-weight:bold;margin:12px 0}}
button{{display:block;width:100%;padding:9px;margin-top:8px;font-size:15px;border:1px solid #888;cursor:pointer}}
.pay{{background:#2e7d32;color:#fff;border-color:#1b5e20}}
.discount{{display:flex;gap:6px;margin:10px 0 4px}}
.discount input{{flex:1;padding:8px;font-size:14px;border:1px solid #888;text-transform:uppercase}}
.discount button{{width:auto;margin:0;padding:8px 12px;font-size:14px}}
.old{{text-decoration:line-through;color:#888;font-size:15px;font-weight:normal;margin-left:6px}}
.ok{{color:#2e7d32;font-size:13px}}.err{{color:#c62828;font-size:13px}}</style></head><body><div class="box">
<div class="warn">Môi trường giả lập - không trừ tiền thật</div>
<h3>Thanh toán vé thuyết minh</h3><div style="font-size:12px;color:#666">Mã giao dịch: {payment_id}</div>
<div class="amount">{amount}</div>{body}</div></body></html>"""


def _vnd(amount: int) -> str:
    return f"{amount:,}".replace(",", ".")


@router.get("/{payment_id}", response_class=HTMLResponse)
async def payment_page(payment_id: str, ok: str = "", err: str = "", service=Depends(get_payment_service)):
    try:
        payment, _ = await service.get_payment(payment_id)
    except AppError as exc:
        return HTMLResponse(
            PAGE.format(payment_id=html.escape(payment_id), amount="—", body=f"<p>{html.escape(exc.message)}</p>"),
            status_code=404,
        )
    base = service.base_price()
    amount = f"{_vnd(payment.amount)} đ"
    if payment.amount < base:
        amount += f"<span class='old'>{_vnd(base)} đ</span>"
    if payment.status != "pending":
        body = f"<p>Giao dịch đã xử lý: <b>{payment.status}</b></p><a href='/'>Về ứng dụng</a>"
    else:
        notice = ""
        if ok:
            notice = f"<div class='ok'>{html.escape(ok)}</div>"
        elif err:
            notice = f"<div class='err'>{html.escape(err)}</div>"
        body = (
            f"<form class='discount' method='post' action='/mock-gateway/{payment.id}/discount'>"
            "<input name='code' maxlength='30' autocomplete='off' placeholder='Mã giảm giá' required>"
            "<button type='submit'>Áp dụng</button></form>"
            f"{notice}"
            f"<form method='post' action='/mock-gateway/{payment.id}/complete'>"
            "<button class='pay' name='result' value='success'>Thanh toán thành công</button>"
            "<button class='cancel' name='result' value='failed'>Hủy / thất bại</button></form>"
        )
    return HTMLResponse(PAGE.format(payment_id=payment.id, amount=amount, body=body))


@router.post("/{payment_id}/discount")
async def apply_discount(payment_id: str, code: str = Form(""), service=Depends(get_payment_service)):
    try:
        _, percent = await service.apply_discount(payment_id, code)
        query = "ok=" + quote(f"Đã áp mã {code.strip().upper()}: giảm {percent}%")
    except AppError as exc:
        query = "err=" + quote(exc.message)
    return RedirectResponse(f"/mock-gateway/{quote(payment_id)}?{query}", status_code=303)


@router.post("/{payment_id}/complete")
async def complete_payment(
    payment_id: str, result: str = Form(...), service=Depends(get_payment_service), state=Depends(get_state)
):
    payload = {
        "payment_id": payment_id,
        "status": "success" if result == "success" else "failed",
        "provider_ref": f"MOCK-{uuid.uuid4().hex[:10].upper()}",
    }
    signature = state.payment_gateway.sign(payload)  # cổng thật sẽ tự ký bằng secret chung
    payment = await service.handle_webhook(payload, signature)
    _, session = await service.get_payment(payment.id)
    if payment.status == "success":
        return RedirectResponse(f"/?payment=success&code={session.code}", status_code=303)
    return RedirectResponse("/?payment=failed", status_code=303)
