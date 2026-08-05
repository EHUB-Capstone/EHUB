// @ts-nocheck
import { useState } from 'react';
import toast from 'react-hot-toast';
import { X, Loader2 } from 'lucide-react';
import { classApi } from '../../api/classApi';
import { PROGRAM_GROUPS } from '../../constants/majors';
import { parseApiError } from '../../utils/apiError';

export default function AddStudentModal({ classId, onClose, onAdded }) {
  const [form, setForm] = useState({
    studentCode: '',
    fullName: '',
    email: '',
    majorCode: ''
  });
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!form.studentCode || !form.fullName || !form.email || !form.majorCode) {
      toast.error('Vui lòng điền mã SV, tên và email');
      return;
    }
    
    setSubmitting(true);
    try {
      await classApi.addStudent(classId, form);
      toast.success('Thêm sinh viên thành công');
      onAdded();
    } catch (err) {
      toast.error(parseApiError(err, 'Thêm sinh viên thất bại').message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-float w-full max-w-md animate-scale-in">
        <div className="flex items-center justify-between p-6 border-b border-slate-100">
          <div>
            <h2 className="text-xl font-bold text-slate-900">Thêm Sinh viên</h2>
            <p className="text-sm text-slate-400 mt-0.5">Thêm thủ công 1 sinh viên vào lớp</p>
          </div>
          <button onClick={onClose} className="p-2 rounded-xl text-slate-400 hover:text-slate-600 hover:bg-slate-100 transition-all">
            <X className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">Mã SV *</label>
            <input
              type="text"
              value={form.studentCode}
              onChange={(e) => setForm({ ...form, studentCode: e.target.value.toUpperCase() })}
              placeholder="VD: SE123456"
              className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary uppercase"
              required
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">Họ và tên *</label>
            <input
              type="text"
              value={form.fullName}
              onChange={(e) => setForm({ ...form, fullName: e.target.value })}
              placeholder="VD: Nguyễn Văn A"
              className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
              required
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">Email *</label>
            <input
              type="email"
              value={form.email}
              onChange={(e) => setForm({ ...form, email: e.target.value })}
              placeholder="VD: anvse123456@fpt.edu.vn"
              className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
              required
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">Chuyên ngành</label>
            <select
              value={form.majorCode}
              onChange={(e) => setForm({ ...form, majorCode: e.target.value })}
              className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary"
            >
              <option value="">-- Chưa chọn --</option>
              {PROGRAM_GROUPS.map(group => (
                <optgroup key={group.code} label={`${group.code} - ${group.name}`}>
                  {group.majors.map(m => (
                    <option key={m.code} value={m.code}>{m.code} - {m.name}</option>
                  ))}
                </optgroup>
              ))}
            </select>
          </div>

          <div className="flex gap-3 pt-4 border-t border-slate-100">
            <button type="button" onClick={onClose} className="flex-1 px-4 py-2.5 border border-slate-200 rounded-xl text-sm text-slate-600 hover:bg-slate-50 transition-all">
              Hủy
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="flex-1 px-4 py-2.5 bg-primary text-white rounded-xl text-sm font-medium hover:bg-primary-700 disabled:opacity-50 transition-all flex items-center justify-center gap-2"
            >
              {submitting ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Thêm sinh viên'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
