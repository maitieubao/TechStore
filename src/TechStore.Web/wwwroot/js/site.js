// TechStore - Main JS

document.addEventListener("DOMContentLoaded", function () {
    // 1. Auto-dismiss alerts
    var alerts = document.querySelectorAll('.alert-dismissible');
    alerts.forEach(function (alert) {
        setTimeout(function () {
            var bsAlert = new bootstrap.Alert(alert);
            bsAlert.close();
        }, 4000);
    });

    // 2. Initialize Wishlist Icons
    if (window.userWishlist) {
        window.userWishlist.forEach(id => {
            const btns = document.querySelectorAll(`.wishlist-btn[data-id="${id}"] i`);
            btns.forEach(icon => {
                icon.classList.remove('bi-heart');
                icon.classList.add('bi-heart-fill', 'text-danger');
            });
        });
    }

    // 3. Attach Wishlist Event Listeners
    document.body.addEventListener('click', function(e) {
        const btn = e.target.closest('.wishlist-btn');
        if (btn) {
            e.preventDefault();
            const productId = btn.getAttribute('data-id');
            const isAuthenticated = btn.getAttribute('data-auth') === 'true';
            
            if (!isAuthenticated) {
                window.location.href = '/Account/Login?returnUrl=' + encodeURIComponent(window.location.pathname);
                return;
            }
            toggleWishlist(btn, productId);
        }
    });
});

async function toggleWishlist(btn, productId) {
    if (!productId) return;
    
    // Optimistic UI update
    const icon = btn.querySelector('i');
    const isActive = icon.classList.contains('bi-heart-fill');
    
    // Toggle immediately
    if (isActive) {
        icon.classList.remove('bi-heart-fill', 'text-danger');
        icon.classList.add('bi-heart');
    } else {
        icon.classList.remove('bi-heart');
        icon.classList.add('bi-heart-fill', 'text-danger');
    }

    try {
        // Find anti-forgery token from any form on page
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        const headers = {};
        if (tokenInput) {
            headers['RequestVerificationToken'] = tokenInput.value;
        }

        const response = await fetch(`/Wishlist/Toggle?productId=${productId}`, {
            method: 'POST',
            headers: headers
        });
        
        if (response.ok) {
            const data = await response.json();
            if (data.success) {
                // Correct UI based on server response if needed
                if (data.isAdded) {
                    icon.classList.remove('bi-heart');
                    icon.classList.add('bi-heart-fill', 'text-danger');
                    showToast('Đã thêm sản phẩm vào yêu thích', 'success');
                } else {
                    icon.classList.remove('bi-heart-fill', 'text-danger');
                    icon.classList.add('bi-heart');
                    showToast('Đã xóa sản phẩm khỏi yêu thích', 'success');
                }
            } else {
                // Revert
                if (isActive) {
                    icon.classList.remove('bi-heart');
                    icon.classList.add('bi-heart-fill', 'text-danger');
                } else {
                    icon.classList.remove('bi-heart-fill', 'text-danger');
                    icon.classList.add('bi-heart');
                }
                showToast('Lỗi khi cập nhật danh sách yêu thích', 'error');
            }
        } else {
             if (response.status === 401) {
                window.location.href = '/Account/Login'; 
             } else {
                showToast('Lỗi kết nối', 'error');
             }
        }
    } catch (e) {
        console.error(e);
        showToast('Lỗi hệ thống', 'error');
    }
}

function showToast(message, type) {
    // Create toast container if not exists
    let container = document.querySelector('.toast-container-js');
    if (!container) {
        container = document.createElement('div');
        container.className = 'toast-fixed toast-container-js';
        document.body.appendChild(container); // Append to body, css .toast-fixed handles position
    }

    const toastHtml = `
        <div class="alert alert-${type === 'success' ? 'success' : 'danger'} alert-dismissible fade show d-flex align-items-center py-2 px-3 shadow" role="alert">
            <i class="bi bi-${type === 'success' ? 'check-circle-fill' : 'exclamation-triangle-fill'} me-2"></i> ${message}
            <button type="button" class="btn-close btn-close-white ms-3" data-bs-dismiss="alert"></button>
        </div>
    `;
    
    const div = document.createElement('div');
    div.innerHTML = toastHtml;
    const toastEl = div.firstElementChild;
    container.appendChild(toastEl);

    // Auto dismiss
    setTimeout(() => {
        const bsAlert = new bootstrap.Alert(toastEl);
        bsAlert.close();
    }, 3000);
}
