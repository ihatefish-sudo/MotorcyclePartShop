/* ============================================================================
    MotorcyclePartShop - site.js
    - Xử lý cart (mock)
    - Toast notification
    - Product gallery
    - Compare list
    - Coupon copy
    - Form validation
============================================================================ */

/* ----------------- Toast Notification ----------------- */
function showToast(message, type = "success") {
    const toast = document.createElement("div");
    toast.className = type === "success" ? "toast-success" : "toast-error";
    toast.style.position = "fixed";
    toast.style.top = "20px";
    toast.style.right = "20px";
    toast.style.zIndex = "9999";
    toast.style.transition = "300ms ease";
    toast.innerText = message;

    document.body.appendChild(toast);

    setTimeout(() => {
        toast.style.opacity = "0";
        toast.style.transform = "translateY(-10px)";
    }, 1800);

    setTimeout(() => toast.remove(), 2400);
}

/* ----------------- CART MOCK ----------------- */
let cart = JSON.parse(localStorage.getItem("mps_cart") || "[]");

function saveCart() {
    localStorage.setItem("mps_cart", JSON.stringify(cart));
}

function addToCart(productId, title, price, img) {
    const item = cart.find(p => p.id === productId);

    if (item) {
        item.qty += 1;
    } else {
        cart.push({ id: productId, title, price, img, qty: 1 });
    }

    saveCart();
    updateCartBadge();
    showToast("Đã thêm vào giỏ hàng!");
}

function removeFromCart(productId) {
    cart = cart.filter(item => item.id !== productId);
    saveCart();
    updateCartBadge();
    renderCart();
    showToast("Đã xoá sản phẩm khỏi giỏ hàng", "error");
}

function updateQty(productId, qty) {
    const item = cart.find(p => p.id === productId);
    if (!item) return;

    qty = parseInt(qty);
    if (qty <= 0) qty = 1;

    item.qty = qty;
    saveCart();
    renderCart();
}

/* ----------------- Cart Badge ----------------- */
function updateCartBadge() {
    const badge = document.querySelector("#cart-count");
    if (badge) {
        const totalQty = cart.reduce((sum, item) => sum + item.qty, 0);
        badge.innerText = totalQty;
    }
}

/* ----------------- Render Cart (Cart page) ----------------- */
function renderCart() {
    const table = document.querySelector("#cart-table-body");
    if (!table) return;

    table.innerHTML = "";

    cart.forEach(item => {
        table.innerHTML += `
            <tr>
                <td><img src="${item.img}" class="small-thumb" /></td>
                <td>${item.title}</td>
                <td>${item.price.toLocaleString()}₫</td>
                <td>
                    <input type="number" class="qty-input" 
                           onchange="updateQty(${item.id}, this.value)"
                           value="${item.qty}">
                </td>
                <td>${(item.qty * item.price).toLocaleString()}₫</td>
                <td>
                    <button class="btn btn-sm btn-danger" onclick="removeFromCart(${item.id})">
                        Xoá
                    </button>
                </td>
            </tr>
        `;
    });

    renderCartTotal();
}

function renderCartTotal() {
    const totalArea = document.querySelector("#cart-total");
    if (!totalArea) return;

    const total = cart.reduce((sum, item) => sum + (item.qty * item.price), 0);
    totalArea.innerHTML = total.toLocaleString() + "₫";
}

/* ----------------- Product Image Gallery ----------------- */
function switchImage(src) {
    const main = document.querySelector("#main-product-img");
    if (main) {
        main.style.opacity = 0;
        setTimeout(() => {
            main.src = src;
            main.style.opacity = 1;
        }, 150);
    }
}

/* ----------------- Compare List ----------------- */
let compareList = [];

function toggleCompare(productId, title) {
    if (compareList.includes(productId)) {
        compareList = compareList.filter(id => id !== productId);
        showToast("Đã xoá khỏi danh sách so sánh", "error");
    } else {
        if (compareList.length >= 3) {
            showToast("Bạn chỉ có thể so sánh tối đa 3 sản phẩm", "error");
            return;
        }
        compareList.push(productId);
        showToast("Đã thêm vào so sánh!");
    }
    updateCompareUI();
}

function updateCompareUI() {
    const badge = document.querySelector("#compare-count");
    if (badge) badge.innerText = compareList.length;
}

/* ----------------- Copy Coupon ----------------- */
function copyCoupon(code) {
    navigator.clipboard.writeText(code).then(() => {
        showToast("Đã sao chép mã: " + code);
    });
}

/* ----------------- Checkout Validation ----------------- */
function validateCheckout() {
    const name = document.querySelector("#chk-name");
    const phone = document.querySelector("#chk-phone");
    const addr = document.querySelector("#chk-addr");

    if (!name.value || !phone.value || !addr.value) {
        showToast("Vui lòng nhập đầy đủ thông tin!", "error");
        return false;
    }

    showToast("Đặt hàng thành công!");
    return true;
}

/* ----------------- Smooth Scroll ----------------- */
function smoothScrollTo(selector) {
    const target = document.querySelector(selector);
    if (target) {
        window.scrollTo({
            top: target.offsetTop - 80,
            behavior: "smooth"
        });
    }
}

/* ----------------- On Page Load ----------------- */
document.addEventListener("DOMContentLoaded", () => {
    updateCartBadge();
    renderCart();
    updateCompareUI();
});
