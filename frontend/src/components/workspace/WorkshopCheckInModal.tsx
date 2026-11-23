// @ts-nocheck
import { useState } from 'react';
import { X, Upload, Image, Loader } from 'lucide-react';
import toast from 'react-hot-toast';
import { chatApi } from '../../api/chatApi';
import { workshopApi } from '../../api/workshopApi';
import { useTranslation } from 'react-i18next';

export default function WorkshopCheckInModal({ isOpen, onClose, workshop, onCheckInSuccess }) {
  const { t } = useTranslation('workspace');
  const [file, setFile] = useState(null);
  const [preview, setPreview] = useState(null);
  const [uploading, setUploading] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  if (!isOpen) return null;

  const handleFileChange = (e) => {
    const selectedFile = e.target.files[0];
    if (!selectedFile) return;

    if (!selectedFile.type.startsWith('image/')) {
      toast.error(t('workshopCheckInModal.pleaseSelectAnImageFile'));
      return;
    }

    if (selectedFile.size > 10 * 1024 * 1024) {
      toast.error(t('workshopCheckInModal.maximumImageSizeIs10mb'));
      return;
    }

    setFile(selectedFile);
    const reader = new FileReader();
    reader.onloadend = () => {
      setPreview(reader.result);
    };
    reader.readAsDataURL(selectedFile);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!file) {
      toast.error(t('workshopCheckInModal.pleaseSelectOrCaptureAnEvidence'));
      return;
    }

    setUploading(true);
    setSubmitting(true);
    try {
      // 1. Upload the image file first via chatApi.uploadFile which uses /chat/upload
      const formData = new FormData();
      formData.append('file', file);

      const uploadRes = await chatApi.uploadFile(formData);
      const imageUrl = uploadRes.data?.url || uploadRes.url;

      if (!imageUrl) {
        throw new Error('Could not retrieve image URL after upload.');
      }

      // 2. Submit the attendance record
      await workshopApi.checkIn(workshop._id, {
        evidenceUrl: imageUrl,
        classId: workshop.classId?._id || workshop.classId,
      });

      toast.success(t('workshopCheckInModal.checkInSuccessfulAwaitingLecturerApproval'));
      onCheckInSuccess?.();
      onClose();
    } catch (err) {
      console.error(err);
      toast.error(err.response?.data?.message || t('workshopCheckInModal.anErrorOccurredDuringCheckIn'));
    } finally {
      setUploading(false);
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      {/* Overlay */}
      <div
        className="absolute inset-0 bg-black/60 backdrop-blur-sm transition-opacity"
        onClick={onClose}
      />

      {/* Modal Container */}
      <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-lg mx-4 overflow-hidden border border-slate-100 transform transition-all">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-5 border-b border-slate-100 bg-slate-50/50">
          <div>
            <h2 className="text-xl font-bold text-slate-800">{t('workshopCheckInModal.checkInWorkshop')}</h2>
            <p className="text-xs text-slate-500 mt-0.5 truncate max-w-sm">
              {workshop?.title}
            </p>
          </div>
          <button
            onClick={onClose}
            className="p-2 rounded-xl hover:bg-slate-100 text-slate-400 hover:text-slate-600 transition-colors"
          >
            <X size={20} />
          </button>
        </div>

        {/* Body */}
        <form onSubmit={handleSubmit} className="p-6 space-y-6">
          <div className="space-y-2">
            <label className="block text-sm font-semibold text-slate-700">
              {t('workshopCheckInModal.uploadEvidencePhoto')} <span className="text-rose-500">*</span>
            </label>
            <p className="text-xs text-slate-400">
              {t('workshopCheckInModal.takeAScreenshotOrPhotoAt')}
            </p>
          </div>

          {/* Upload Box */}
          <div className="group relative border-2 border-dashed border-slate-200 hover:border-primary/50 hover:bg-slate-50/50 rounded-2xl transition-all overflow-hidden min-h-[220px] flex flex-col items-center justify-center p-4">
            {preview ? (
              <div className="relative w-full h-[200px]">
                <img
                  src={preview}
                  alt={t('workshopCheckInModal.evidencePreview')}
                  className="w-full h-full object-contain rounded-xl"
                />
                <button
                  type="button"
                  onClick={() => {
                    setFile(null);
                    setPreview(null);
                  }}
                  className="absolute top-2 right-2 p-1.5 bg-black/70 hover:bg-black/90 text-white rounded-lg transition-colors shadow-md"
                >
                  <X size={16} />
                </button>
              </div>
            ) : (
              <label className="w-full h-full cursor-pointer flex flex-col items-center justify-center py-8 space-y-3">
                <div className="p-4 bg-slate-50 rounded-2xl group-hover:bg-primary/5 group-hover:scale-105 transition-all text-slate-400 group-hover:text-primary-700">
                  <Image className="w-8 h-8" />
                </div>
                <div className="text-center">
                  <p className="text-sm font-bold text-slate-700">{t('workshopCheckInModal.selectOrCaptureEvidencePhoto')}</p>
                  <p className="text-xs text-slate-400 mt-1">{t('workshopCheckInModal.pngJpgOrJpegUpTo')}</p>
                </div>
                <input
                  type="file"
                  accept="image/*"
                  onChange={handleFileChange}
                  className="hidden"
                />
              </label>
            )}
          </div>

          {/* Footer Actions */}
          <div className="flex items-center justify-end gap-3 pt-4 border-t border-slate-100">
            <button
              type="button"
              onClick={onClose}
              disabled={submitting}
              className="px-5 py-2.5 text-sm font-semibold text-slate-600 bg-slate-50 border border-slate-200/60 rounded-xl hover:bg-slate-100 hover:text-slate-800 transition-colors disabled:opacity-50"
            >
              {t('common:actions.cancel')}
            </button>
            <button
              type="submit"
              disabled={submitting || !file}
              className="flex items-center gap-2 px-6 py-2.5 text-sm font-bold text-white bg-primary rounded-xl hover:shadow-glow-primary active:scale-[0.98] transition-all disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {submitting ? (
                <>
                  <Loader className="w-4 h-4 animate-spin" />
                  <span>{t('workshopCheckInModal.processing')}</span>
                </>
              ) : (
                <>
                  <Upload className="w-4 h-4" />
                  <span>{t('workshopCheckInModal.confirmCheckIn')}</span>
                </>
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
