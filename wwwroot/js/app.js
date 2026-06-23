// ---------- helpers ----------
const $$ = (sel) => document.querySelectorAll(sel);
const el = (id) => document.getElementById(id);

// Escape any user/data-supplied string before it goes into markup (prevents XSS).
function esc(v) {
    return String(v ?? "").replace(/[&<>"']/g, (c) => (
        { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]
    ));
}
// Render trusted-template markup (data already escaped via esc()) without using innerHTML.
function paint(node, markup) {
    node.replaceChildren();
    node.insertAdjacentHTML("beforeend", markup);
}

function toast(msg, type = "") {
    const t = el("toast");
    t.textContent = msg;
    t.className = "toast " + type;
    setTimeout(() => t.classList.add("hidden"), 2600);
}
const money = (n) => "Rs " + Number(n).toLocaleString("en-IN", { minimumFractionDigits: 0 });
const fmtDate = (d) => new Date(d).toLocaleString();

// Inline SVG icon reference (symbols defined in index.html sprite).
const ico = (id) => `<svg class="icon" aria-hidden="true"><use href="#${id}"></use></svg>`;
const stars = (avg, count) => count > 0
    ? `<span class="stars">${ico("i-star")} ${Math.round(avg * 10) / 10} (${count})</span>`
    : `<span class="desc">No reviews yet</span>`;
const pin = (city) => `<span class="row-icon">${ico("i-pin")} ${esc(city)}</span>`;

// Map a category name to its sprite icon id.
const CAT_ICONS = {
    plumbing: "c-plumbing", electrical: "c-electrical", cleaning: "c-cleaning",
    painting: "c-painting", carpentry: "c-carpentry", "appliance repair": "c-appliance"
};
const catIcon = (name) => CAT_ICONS[String(name || "").toLowerCase()] || "c-default";

let categories = [];
let activeChip = "";
let searchController = null;   // cancels superseded service searches

function debounce(fn, ms = 400) {
    let t;
    return (...args) => { clearTimeout(t); t = setTimeout(() => fn(...args), ms); };
}

// Disable a submit button + show a busy label while an async action runs, then restore.
// Prevents double-submit. busyLabel is a controlled literal (safe to set as text).
async function submitting(btn, busyLabel, fn) {
    if (!btn) return fn();
    const original = btn.innerHTML;
    btn.disabled = true;
    btn.textContent = busyLabel;
    try { return await fn(); }
    finally { btn.disabled = false; btn.innerHTML = original; }
}

// ---------- auth UI ----------
function refreshHeader() {
    const user = API.getUser();
    const logged = !!user;
    el("loginBtn").classList.toggle("hidden", logged);
    el("registerBtn").classList.toggle("hidden", logged);
    el("logoutBtn").classList.toggle("hidden", !logged);
    const badge = el("userBadge");
    badge.classList.toggle("hidden", !logged);
    if (logged) badge.textContent = `${user.name} · ${user.role}`;
}

function openAuth(tab = "login") {
    el("authModal").classList.remove("hidden");
    switchAuthTab(tab);
    el("authError").textContent = "";
}
function closeAuth() { el("authModal").classList.add("hidden"); }
function switchAuthTab(tab) {
    $$("[data-auth-tab]").forEach(b => b.classList.toggle("active", b.dataset.authTab === tab));
    el("loginForm").classList.toggle("hidden", tab !== "login");
    el("registerForm").classList.toggle("hidden", tab !== "register");
}

// ---------- tabs / views ----------
const VIEWS = {
    browse:      { label: "Browse services", roles: ["*"] },
    mybookings:  { label: "My bookings",     roles: ["Customer"] },
    mylistings:  { label: "My listings",     roles: ["Provider"] },
    jobs:        { label: "Job requests",    roles: ["Provider"] },
    kyc:         { label: "KYC",             roles: ["Provider"] },
    admin:       { label: "Dashboard",       roles: ["Admin"] },
};

function buildTabs() {
    const user = API.getUser();
    const role = user?.role;
    const bar = el("appTabs");
    bar.replaceChildren();
    for (const [key, def] of Object.entries(VIEWS)) {
        if (!def.roles.includes("*") && (!role || !def.roles.includes(role))) continue;
        const b = document.createElement("button");
        b.textContent = def.label;
        b.dataset.view = key;
        b.onclick = () => showView(key);
        bar.appendChild(b);
    }
    showView("browse");
}

function showView(key) {
    $$(".view").forEach(v => v.classList.add("hidden"));
    el("view-" + key)?.classList.remove("hidden");
    $$("#appTabs button").forEach(b => b.classList.toggle("active", b.dataset.view === key));
    if (key === "browse") loadListings();
    if (key === "mybookings") loadMyBookings();
    if (key === "mylistings") loadMyListings();
    if (key === "jobs") loadJobs();
    if (key === "kyc") loadKyc();
    if (key === "admin") loadAdmin();
}

// ---------- categories ----------
async function loadCategories() {
    categories = await API.get("/categories");
    const opts = categories.map(c => `<option value="${c.id}">${esc(c.name)}</option>`).join("");
    paint(el("fCategory"), `<option value="">All categories</option>` + opts);
    if (el("listingCategory")) paint(el("listingCategory"), opts);
    renderChips();
}

function renderChips() {
    const wrap = el("categoryChips");
    if (!wrap) return;
    paint(wrap, categories.map(c => `
        <button class="chip ${activeChip == c.id ? "active" : ""}" data-chip="${c.id}">
            ${ico(catIcon(c.name))} ${esc(c.name)}
        </button>`).join(""));
    wrap.querySelectorAll("[data-chip]").forEach(b => b.onclick = () => {
        activeChip = activeChip == b.dataset.chip ? "" : b.dataset.chip;
        el("fCategory").value = activeChip;
        renderChips();
        loadListings();
    });
}

// ---------- browse / search ----------
async function loadListings() {
    const params = new URLSearchParams();
    const kw = el("fKeyword").value.trim();
    const cat = el("fCategory").value;
    const city = el("fCity").value.trim();
    const min = el("fMin").value;
    const max = el("fMax").value;
    if (kw) params.set("keyword", kw);
    if (cat) params.set("categoryId", cat);
    if (city) params.set("city", city);
    if (min) params.set("minPrice", min);
    if (max) params.set("maxPrice", max);

    const wrap = el("listings");
    // Cancel any in-flight search so a slower earlier response can't overwrite this one.
    searchController?.abort();
    searchController = new AbortController();
    const btn = el("searchBtn");
    btn.disabled = true;
    paint(wrap, `<p class="empty">Searching…</p>`);

    let list;
    try {
        list = await API.get("/services?" + params.toString(), { signal: searchController.signal });
    } catch (e) {
        if (e.name === "AbortError") return;   // a newer search took over
        paint(wrap, `<p class="empty">Couldn't load services. Please try again.</p>`);
        toast(e.message, "error");
        return;
    } finally {
        btn.disabled = false;
    }
    if (!list.length) { paint(wrap, `<p class="empty">No services found.</p>`); return; }

    const user = API.getUser();
    const canBook = user?.role === "Customer";
    paint(wrap, list.map(l => `
        <div class="card">
            <span class="cat">${ico(catIcon(l.categoryName))} ${esc(l.categoryName)}</span>
            <h3>${esc(l.title)}</h3>
            <p class="desc">${esc(l.description || "")}</p>
            <div class="meta">
                <span class="price">${money(l.price)}</span>
                ${stars(l.averageRating, l.reviewCount)}
            </div>
            <p class="desc">${pin(l.city)} · by ${esc(l.providerName)}${l.providerVerified ? ` <span class="verified">${ico("i-shield")} Verified</span>` : ""}</p>
            <div class="card-actions">
                ${canBook ? `<button class="btn sm icon-btn" data-book="${l.id}" data-title="${esc(l.title)}">${ico("i-calendar")} Book now</button>` : ""}
                <button class="btn ghost sm icon-btn" data-reviews="${l.id}">${ico("i-star")} Reviews</button>
            </div>
        </div>`).join(""));

    wrap.querySelectorAll("[data-book]").forEach(b =>
        b.onclick = () => bookService(+b.dataset.book, b.dataset.title));
    wrap.querySelectorAll("[data-reviews]").forEach(b =>
        b.onclick = () => viewReviews(+b.dataset.reviews));
}

async function bookService(id, title) {
    if (!API.getToken()) return openAuth("login");
    const when = prompt(`Book "${title}".\nEnter date & time (YYYY-MM-DD HH:MM):`);
    if (!when) return;
    const scheduled = new Date(when.replace(" ", "T"));
    if (isNaN(scheduled)) return toast("Invalid date format.", "error");
    const address = prompt("Service address:");
    if (!address) return;
    const notes = prompt("Notes (optional):") || null;
    try {
        await API.post("/bookings", {
            serviceListingId: id,
            scheduledAt: scheduled.toISOString(),
            address, notes
        });
        toast("Booking requested!", "success");
        showView("mybookings");
    } catch (e) { toast(e.message, "error"); }
}

async function viewReviews(listingId) {
    const reviews = await API.get(`/reviews/listing/${listingId}`);
    if (!reviews.length) return toast("No reviews yet.");
    alert(reviews.map(r => `★${r.rating} — ${r.customerName}\n${r.comment || ""}`).join("\n\n"));
}

// ---------- customer bookings ----------
async function loadMyBookings() {
    const list = await API.get("/bookings");
    const wrap = el("myBookings");
    if (!list.length) { paint(wrap, `<p class="empty">No bookings yet.</p>`); return; }
    paint(wrap, list.map(b => `
        <div class="card">
            <div class="info">
                <h3>${esc(b.serviceTitle)}</h3>
                <p class="desc"><span class="row-icon">${ico("i-calendar")} ${esc(fmtDate(b.scheduledAt))}</span> · ${pin(b.address)}</p>
                <p class="desc">Provider: ${esc(b.providerName)} · ${money(b.amount)}</p>
                <span class="pill ${esc(b.status)}">${esc(b.status)}</span>
                <span class="pill ${esc(b.paymentStatus)}">${esc(b.paymentStatus)}</span>
            </div>
            <div class="card-actions">
                ${b.paymentStatus !== "Paid" && !["Rejected","Cancelled"].includes(b.status)
                    ? `<button class="btn success sm icon-btn" data-pay="${b.id}">${ico("i-wallet")} Pay</button>` : ""}
                ${!["Completed","Rejected","Cancelled"].includes(b.status)
                    ? `<button class="btn warn sm" data-cancel="${b.id}">Cancel</button>` : ""}
                ${b.status === "Completed"
                    ? `<button class="btn sm icon-btn" data-review="${b.id}">${ico("i-star")} Review</button>` : ""}
            </div>
        </div>`).join(""));

    wrap.querySelectorAll("[data-pay]").forEach(b => b.onclick = () => payBooking(+b.dataset.pay));
    wrap.querySelectorAll("[data-cancel]").forEach(b => b.onclick = () => cancelBooking(+b.dataset.cancel));
    wrap.querySelectorAll("[data-review]").forEach(b => b.onclick = () => reviewBooking(+b.dataset.review));
}

async function payBooking(id) {
    const method = prompt("Payment method (eSewa / Khalti / Cash):", "eSewa");
    if (!method) return;
    try {
        await API.post(`/bookings/${id}/pay`, { method });
        toast("Payment successful!", "success");
        loadMyBookings();
    } catch (e) { toast(e.message, "error"); }
}
async function cancelBooking(id) {
    if (!confirm("Cancel this booking?")) return;
    try { await API.post(`/bookings/${id}/cancel`); toast("Booking cancelled."); loadMyBookings(); }
    catch (e) { toast(e.message, "error"); }
}
async function reviewBooking(id) {
    const rating = parseInt(prompt("Rating (1-5):", "5"), 10);
    if (!rating || rating < 1 || rating > 5) return toast("Rating must be 1-5.", "error");
    const comment = prompt("Comment (optional):") || null;
    try { await API.post("/reviews", { bookingId: id, rating, comment }); toast("Thanks for your review!", "success"); }
    catch (e) { toast(e.message, "error"); }
}

// ---------- provider listings ----------
async function loadMyListings() {
    const list = await API.get("/services/mine");
    const wrap = el("myListings");
    if (!list.length) { paint(wrap, `<p class="empty">No listings yet.</p>`); return; }
    paint(wrap, list.map(l => `
        <div class="card">
            <span class="cat">${ico(catIcon(l.categoryName))} ${esc(l.categoryName)}</span>
            <h3>${esc(l.title)} ${l.isActive ? "" : "<small>(inactive)</small>"}</h3>
            <p class="desc">${esc(l.description || "")}</p>
            <div class="meta"><span class="price">${money(l.price)}</span>
                ${stars(l.averageRating, l.reviewCount)}</div>
            <p class="desc">${pin(l.city)}</p>
            <div class="card-actions">
                <button class="btn ghost sm" data-edit="${l.id}">Edit</button>
                <button class="btn danger sm" data-del="${l.id}">Delete</button>
            </div>
        </div>`).join(""));

    wrap.querySelectorAll("[data-edit]").forEach(b =>
        b.onclick = () => editListing(list.find(x => x.id === +b.dataset.edit)));
    wrap.querySelectorAll("[data-del]").forEach(b =>
        b.onclick = () => deleteListing(+b.dataset.del));
}

function showListingForm(show) {
    el("listingForm").classList.toggle("hidden", !show);
    if (!show) el("listingForm").reset();
}
function editListing(l) {
    showListingForm(true);
    const f = el("listingForm");
    f.id.value = l.id;
    f.title.value = l.title;
    f.description.value = l.description || "";
    f.price.value = l.price;
    f.city.value = l.city;
    f.categoryId.value = l.categoryId;
    f.isActive.checked = l.isActive;
    window.scrollTo({ top: 0, behavior: "smooth" });
}
async function deleteListing(id) {
    if (!confirm("Delete this listing?")) return;
    try { await API.del(`/services/${id}`); toast("Listing deleted."); loadMyListings(); }
    catch (e) { toast(e.message, "error"); }
}

// ---------- provider jobs ----------
async function loadJobs() {
    const list = await API.get("/bookings");
    const wrap = el("providerJobs");
    if (!list.length) { paint(wrap, `<p class="empty">No job requests yet.</p>`); return; }
    paint(wrap, list.map(b => `
        <div class="card">
            <div class="info">
                <h3>${esc(b.serviceTitle)}</h3>
                <p class="desc"><span class="row-icon">${ico("i-calendar")} ${esc(fmtDate(b.scheduledAt))}</span> · ${pin(b.address)}</p>
                <p class="desc">Customer: ${esc(b.customerName)} · ${money(b.amount)} · ${esc(b.notes || "")}</p>
                <span class="pill ${esc(b.status)}">${esc(b.status)}</span>
                <span class="pill ${esc(b.paymentStatus)}">${esc(b.paymentStatus)}</span>
            </div>
            <div class="card-actions">
                ${b.status === "Pending" ? `
                    <button class="btn success sm icon-btn" data-act="accept" data-id="${b.id}">${ico("i-check")} Accept</button>
                    <button class="btn danger sm icon-btn" data-act="reject" data-id="${b.id}">${ico("i-x")} Reject</button>` : ""}
                ${b.status === "Accepted" ? `<button class="btn sm icon-btn" data-act="complete" data-id="${b.id}">${ico("i-check")} Mark complete</button>` : ""}
            </div>
        </div>`).join(""));

    wrap.querySelectorAll("[data-act]").forEach(b =>
        b.onclick = () => jobAction(+b.dataset.id, b.dataset.act));
}
async function jobAction(id, action) {
    try { await API.post(`/bookings/${id}/${action}`); toast(`Booking ${action}ed.`, "success"); loadJobs(); }
    catch (e) { toast(e.message, "error"); }
}

// ---------- provider KYC ----------
const KYC_LABEL = { NotSubmitted: "Not submitted", Pending: "Pending review", Approved: "Approved", Rejected: "Rejected" };

async function loadKyc() {
    let k;
    try { k = await API.get("/kyc/me"); } catch (e) { return toast(e.message, "error"); }
    const cls = { NotSubmitted: "info", Pending: "pending", Approved: "approved", Rejected: "rejected" }[k.status] || "info";
    const text = k.status === "Approved" ? "KYC Verified ✓"
        : k.status === "Pending" ? "Your KYC is under review (usually 1–2 business days)."
        : k.status === "Rejected" ? `KYC Rejected: ${esc(k.rejectionReason || "no reason given")}. Please resubmit below.`
        : "Submit your documents below to get verified.";
    paint(el("kycBanner"), `<div class="banner ${cls}">${text}</div>`);
    await renderKycDocs(el("kycDocs"), k.documents);
}

async function renderKycDocs(container, docs) {
    if (!docs.length) { paint(container, `<p class="empty">No documents submitted.</p>`); return; }
    paint(container, docs.map(d => `
        <div class="kyc-doc" data-docurl="${esc(d.url)}" data-type="${esc(d.docType)}">
            <div class="kyc-thumb">Loading…</div>
            <div class="kyc-cap">${esc(d.docType)}${d.fileName ? ` · ${esc(d.fileName)}` : ""}</div>
        </div>`).join(""));
    container.querySelectorAll(".kyc-doc").forEach(loadDocThumb);
}

// Documents need an auth header, so <img src> can't load them directly — fetch as blob.
async function loadDocThumb(node) {
    const thumb = node.querySelector(".kyc-thumb");
    try {
        const res = await fetch(node.dataset.docurl, { headers: { Authorization: "Bearer " + API.getToken() } });
        if (!res.ok) throw new Error();
        const blob = await res.blob();
        const obj = URL.createObjectURL(blob);
        thumb.replaceChildren();
        if (blob.type === "application/pdf") {
            const a = document.createElement("a");
            a.href = obj; a.target = "_blank"; a.rel = "noopener"; a.className = "pdf-link"; a.textContent = "View PDF";
            thumb.appendChild(a);
        } else {
            const img = document.createElement("img");
            img.src = obj; img.alt = node.dataset.type; img.onclick = () => window.open(obj, "_blank");
            thumb.appendChild(img);
        }
    } catch { thumb.textContent = "Failed to load"; }
}

// ---------- admin KYC review ----------
async function openKycReview(providerId) {
    let k;
    try { k = await API.get(`/admin/kyc/${providerId}`); } catch (e) { return toast(e.message, "error"); }
    el("kycModalTitle").textContent = `${k.providerName} — KYC ${KYC_LABEL[k.status] || k.status}`;
    el("kycModalMeta").textContent = `${k.email}${k.submittedAt ? " · submitted " + fmtDate(k.submittedAt) : ""}`
        + (k.rejectionReason ? ` · last reason: ${k.rejectionReason}` : "");
    await renderKycDocs(el("kycModalDocs"), k.documents);
    const actions = el("kycModalActions");
    if (k.status === "Pending") {
        paint(actions, `<button class="btn success" id="kycApprove">Approve &amp; verify</button>
                        <button class="btn danger" id="kycReject">Reject</button>`);
        el("kycApprove").onclick = () => kycDecision(providerId, "approve");
        el("kycReject").onclick = () => kycDecision(providerId, "reject");
    } else {
        paint(actions, `<p class="desc">No action available — KYC is ${esc(KYC_LABEL[k.status] || k.status)}.</p>`);
    }
    el("kycModal").classList.remove("hidden");
}

async function kycDecision(providerId, action) {
    try {
        if (action === "reject") {
            const reason = prompt("Reason for rejection (shown to the provider):");
            if (!reason) return;
            await API.post(`/admin/kyc/${providerId}/reject`, { reason });
        } else {
            await API.post(`/admin/kyc/${providerId}/approve`, {});
        }
        toast(`KYC ${action === "approve" ? "approved" : "rejected"}.`, "success");
        el("kycModal").classList.add("hidden");
        loadAdmin();
    } catch (e) { toast(e.message, "error"); }
}

// ---------- admin ----------
async function loadAdmin() {
    const s = await API.get("/admin/dashboard");
    const cards = [
        ["customers", "Customers", s.customers], ["providers", "Providers", s.providers],
        ["listings", "Listings", s.listings], ["bookings", "Bookings", s.bookings],
        ["completed", "Completed", s.completed], ["revenue", "Revenue", money(s.paidRevenue)]
    ];
    paint(el("adminStats"), cards.map(([key, label, num]) =>
        `<button class="stat" data-stat="${key}"><div class="num">${esc(num)}</div><div class="label">${esc(label)} ›</div></button>`).join(""));
    el("adminStats").querySelectorAll("[data-stat]").forEach(b => b.onclick = () => openDetail(b.dataset.stat));

    const providers = await API.get("/admin/users?role=1");
    paint(el("adminProviders"), !providers.length ? `<p class="empty">No providers yet.</p>` : providers.map(p => `
        <div class="card">
            <div class="info">
                <h3>${esc(p.fullName)} ${p.isVerified ? `<span class="verified">${ico("i-shield")} Verified</span>` : ""}</h3>
                <p class="desc">${esc(p.email)} · joined ${esc(fmtDate(p.createdAt))}</p>
                <span class="pill kyc-${esc(p.kycStatus)}">KYC: ${esc(KYC_LABEL[p.kycStatus] || p.kycStatus)}</span>
            </div>
            <button class="btn ${p.kycStatus === "Pending" ? "" : "ghost"} sm" data-kyc="${p.id}">Review KYC</button>
        </div>`).join(""));
    el("adminProviders").querySelectorAll("[data-kyc]").forEach(b =>
        b.onclick = () => openKycReview(+b.dataset.kyc));

    paint(el("adminCategories"), categories.map(c => `
        <div class="card"><div class="info"><h3>${esc(c.name)}</h3><p class="desc">${esc(c.description || "")}</p></div>
        <button class="btn danger sm" data-delcat="${c.id}">Delete</button></div>`).join(""));

    el("adminCategories").querySelectorAll("[data-delcat]").forEach(b =>
        b.onclick = () => deleteCategory(+b.dataset.delcat));
}

// Drill-down: clicking a dashboard stat opens a modal listing the underlying records.
async function openDetail(kind) {
    const body = el("detailBody");
    let rows = [], heading = "", render;
    try {
        if (kind === "customers" || kind === "providers") {
            const role = kind === "customers" ? 0 : 1;
            rows = await API.get(`/admin/users?role=${role}`);
            heading = kind === "customers" ? "Customers" : "Providers";
            render = u => `<div class="card"><div class="info">
                <h3>${esc(u.fullName)} ${u.isVerified && kind === "providers" ? `<span class="verified">${ico("i-shield")} Verified</span>` : ""}</h3>
                <p class="desc">${esc(u.email)}${u.phone ? " · " + esc(u.phone) : ""} · joined ${esc(fmtDate(u.createdAt))}</p>
            </div></div>`;
        } else if (kind === "listings") {
            rows = await API.get("/admin/listings");
            heading = "Listings";
            render = l => `<div class="card"><div class="info">
                <h3>${esc(l.title)} ${l.isActive ? "" : "<small>(inactive)</small>"}</h3>
                <p class="desc">${money(l.price)} · ${pin(l.city)} · by ${esc(l.provider)}</p>
            </div></div>`;
        } else {
            rows = await API.get("/admin/bookings");
            if (kind === "completed") rows = rows.filter(b => b.status === "Completed");
            if (kind === "revenue") rows = rows.filter(b => b.payment === "Paid");
            heading = kind === "completed" ? "Completed bookings"
                : kind === "revenue" ? "Paid bookings (revenue)" : "All bookings";
            render = b => `<div class="card"><div class="info">
                <h3>${esc(b.service)}</h3>
                <p class="desc">${esc(b.customer)} → ${esc(b.provider)} · ${esc(fmtDate(b.scheduledAt))}</p>
                <span class="pill ${esc(b.status)}">${esc(b.status)}</span>
                <span class="pill ${esc(b.payment)}">${esc(b.payment)}</span> · ${money(b.amount)}
            </div></div>`;
        }
        el("detailTitle").textContent = `${heading} (${rows.length})`;
        paint(body, rows.length ? rows.map(render).join("") : `<p class="empty">Nothing here yet.</p>`);
        el("detailModal").classList.remove("hidden");
    } catch (e) { toast(e.message, "error"); }
}
async function deleteCategory(id) {
    if (!confirm("Delete category?")) return;
    try { await API.del(`/categories/${id}`); toast("Category deleted."); await loadCategories(); loadAdmin(); }
    catch (e) { toast(e.message, "error"); }
}

// ---------- event wiring ----------
function wire() {
    el("loginBtn").onclick = () => openAuth("login");
    el("registerBtn").onclick = () => openAuth("register");
    el("logoutBtn").onclick = () => { API.clearSession(); refreshHeader(); buildTabs(); toast("Logged out."); };
    $$("[data-close]").forEach(b => b.onclick = closeAuth);
    $$("[data-auth-tab]").forEach(b => b.onclick = () => switchAuthTab(b.dataset.authTab));
    el("authModal").onclick = (e) => { if (e.target.id === "authModal") closeAuth(); };

    const detail = el("detailModal");
    detail.querySelector("[data-detail-close]").onclick = () => detail.classList.add("hidden");
    detail.onclick = (e) => { if (e.target.id === "detailModal") detail.classList.add("hidden"); };

    const km = el("kycModal");
    km.querySelector("[data-kyc-close]").onclick = () => km.classList.add("hidden");
    km.onclick = (e) => { if (e.target.id === "kycModal") km.classList.add("hidden"); };

    el("kycForm").onsubmit = (e) => {
        e.preventDefault();
        const f = e.target;
        submitting(f.querySelector("[type=submit]"), "Uploading…", async () => {
            try {
                await API.upload("/kyc/submit", new FormData(f));
                toast("Submitted for review!", "success");
                f.reset();
                loadKyc();
            } catch (err) { toast(err.message, "error"); }
        });
    };

    el("regRole").onchange = (e) => el("regBio").classList.toggle("hidden", e.target.value !== "1");

    el("loginForm").onsubmit = (e) => {
        e.preventDefault();
        const f = e.target;
        submitting(f.querySelector("[type=submit]"), "Logging in…", async () => {
            try {
                const auth = await API.post("/auth/login", { email: f.email.value, password: f.password.value });
                API.setSession(auth); closeAuth(); refreshHeader(); buildTabs(); toast("Welcome back!", "success");
            } catch (err) { el("authError").textContent = err.message; }
        });
    };
    el("registerForm").onsubmit = (e) => {
        e.preventDefault();
        const f = e.target;
        submitting(f.querySelector("[type=submit]"), "Creating account…", async () => {
            try {
                const auth = await API.post("/auth/register", {
                    fullName: f.fullName.value, email: f.email.value, password: f.password.value,
                    phone: f.phone.value || null, role: parseInt(f.role.value, 10), bio: f.bio.value || null
                });
                API.setSession(auth); closeAuth(); refreshHeader(); buildTabs(); toast("Account created!", "success");
            } catch (err) { el("authError").textContent = err.message; }
        });
    };

    el("searchBtn").onclick = loadListings;
    el("fKeyword").addEventListener("keydown", (e) => { if (e.key === "Enter") loadListings(); });
    el("fKeyword").addEventListener("input", debounce(loadListings, 450));  // type-to-search
    el("fCategory").onchange = (e) => { activeChip = e.target.value; renderChips(); loadListings(); };
    el("clearFilters").onclick = () => {
        ["fKeyword", "fCity", "fMin", "fMax"].forEach(id => el(id).value = "");
        el("fCategory").value = ""; activeChip = ""; renderChips(); loadListings();
    };

    el("newListingBtn").onclick = () => showListingForm(true);
    el("cancelListing").onclick = () => showListingForm(false);
    el("listingForm").onsubmit = (e) => {
        e.preventDefault();
        const f = e.target;
        submitting(f.querySelector("[type=submit]"), "Saving…", async () => {
            const body = {
                title: f.title.value, description: f.description.value || null,
                price: parseFloat(f.price.value), city: f.city.value,
                categoryId: parseInt(f.categoryId.value, 10), isActive: f.isActive.checked
            };
            try {
                if (f.id.value) await API.put(`/services/${f.id.value}`, body);
                else await API.post("/services", body);
                toast("Listing saved.", "success"); showListingForm(false); loadMyListings();
            } catch (err) { toast(err.message, "error"); }
        });
    };

    el("categoryForm").onsubmit = (e) => {
        e.preventDefault();
        const f = e.target;
        submitting(f.querySelector("[type=submit]"), "Adding…", async () => {
            try {
                await API.post("/categories", { name: f.name.value, description: f.description.value || null });
                f.reset(); toast("Category added.", "success"); await loadCategories(); loadAdmin();
            } catch (err) { toast(err.message, "error"); }
        });
    };
}

// ---------- boot ----------
(async function init() {
    wire();
    refreshHeader();
    await loadCategories();
    buildTabs();
})();
