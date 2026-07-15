// @ts-nocheck
import { useState } from 'react';
import toast from 'react-hot-toast';
import { X, Loader2, Pencil } from 'lucide-react';
import { classApi } from '../../api/classApi';

/**
 * RenameClassModal — cho phép ADMIN hoặc Giảng viên phụ trách đổi tên lớp (classCode).
 * Props:
 *   classId       – MongoDB _id của lớp
 *   currentCode   – classCode hiện tại (hiển thị mặc định)
 *   onClose       – callback đóng modal
 *   onRenamed     – callback sau khi đổi tên thành công, nhận { class } từ API
 */
export default function RenameClassModal({ classId, currentCode, onClose, onRenamed }) {
  const [value,      setValue]      = useState(currentCode || '');
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    const trimmed = value.trim().toUpperCase();
    if (!trimmed) { toast.error('Tên lớp không được để trống'); return; }
    if (trimmed === currentCode?.toUpperCase()) { onClose(); return; }

    setSubmitting(true);
    try {
      const res = await classApi.rename(classId, trimmed);
      const updated = res?.data?.class || res?.class;
      toast.success(`Đã đổi tên lớp thành "${trimmed}"`);
      onRenamed(updated);
    } catch (err) {
      const msg = err?.response?.data?.message || err?.message || 'Đổi tên thất bại';
      toast.error(msg);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      {/* Backdrop */}
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />

      <div className="relative bg-white rounded-2xl shadow-float w-full max-w-sm animate-scale-in">
        {/* Header */}
        <div className="flex items-center justify-between p-6 border-b border-slate-100">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-xl bg-primary-100 flex items-center justify-center">
              <Pencil className="w-4 h-4 text-primary" />
            </div>
            <div>
              <h2 className="text-lg font-bold text-slate-900">Đổi tên lớp</h2>
              <p className="text-xs text-slate-400 mt-0.5">Nhập tên lớp mới</p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-2 rounded-xl text-slate-400 hover:text-slate-600 hover:bg-slate-100 transition-all"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Body */}
        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1.5">
              Tên lớp (Class Code)
            </label>
            <input
              id="rename-class-input"
              type="text"
              value={value}
              onChange={(e) => setValue(e.target.value)}
              placeholder="VD: EXE101_10"
              autoFocus
              className="w-full border border-slate-200 rounded-xl px-3 py-2.5 text-sm font-mono uppercase outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all"
            />
            <p className="text-xs text-slate-400 mt-1.5">
              Tên lớp sẽ tự động chuyển thành chữ hoa.
            </p>
          </div>

          {/* Preview */}
          {value.trim() && value.trim().toUpperCase() !== currentCode?.toUpperCase() && (
            <div className="bg-primary-50 rounded-xl p-3 border border-primary-100">
              <p className="text-xs text-slate-500 mb-1">Xem trước thay đổi</p>
              <div className="flex items-center gap-2 text-sm font-mono">
                <span className="text-slate-400 line-through">{currentCode}</span>
                <span className="text-slate-400">→</span>
                <span className="text-primary font-semibold">{value.trim().toUpperCase()}</span>
              </div>
            </div>
          )}

          {/* Footer */}
          <div className="flex gap-3 pt-1">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 px-4 py-2.5 border border-slate-200 rounded-xl text-sm text-slate-600 hover:bg-slate-50 transition-all"
            >
              Hủy
            </button>
            <button
              type="submit"
              disabled={submitting || !value.trim()}
              className="flex-1 px-4 py-2.5 bg-primary text-white rounded-xl text-sm font-medium hover:bg-primary-700 disabled:opacity-50 transition-all flex items-center justify-center gap-2"
            >
              {submitting ? (
                <><Loader2 className="w-4 h-4 animate-spin" /> Đang lưu...</>
              ) : (
                'Lưu tên mới'
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
