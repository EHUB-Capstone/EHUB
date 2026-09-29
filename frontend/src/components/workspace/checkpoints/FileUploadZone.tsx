// @ts-nocheck
// src/components/workspace/checkpoints/FileUploadZone.jsx
import { useRef, useState } from 'react';
import { UploadCloud, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { checkpointApi } from '../../../api/checkpointApi';
import {
  checkpointUploadFailureMessage,
  validateCheckpointUploadFile,
} from '../../../utils/checkpointUpload';

export default function FileUploadZone({ teamId, checkpointNumber, onUploaded, variant = 'default' }) {
  const isLarge = variant === 'large';
  const [dragging,   setDragging]   = useState(false);
  const [uploading,  setUploading]  = useState(false);
  const [uploadState, setUploadState] = useState(null);
  const fileRef = useRef(null);
  const uploadInProgressRef = useRef(false);

  const processFiles = async (rawFiles) => {
    const files = Array.from(rawFiles);
    if (!files.length || uploadInProgressRef.current) return;

    const validFiles = [];
    for (const file of files) {
      const validationMessage = validateCheckpointUploadFile(file);
      if (validationMessage) {
        toast.error(validationMessage);
      } else {
        validFiles.push(file);
      }
    }
    if (!validFiles.length) return;

    uploadInProgressRef.current = true;
    setUploading(true);
    let uploadedCount = 0;
    try {
      for (let index = 0; index < validFiles.length; index += 1) {
        const file = validFiles[index];
        setUploadState({ fileName: file.name, index: index + 1, total: validFiles.length, percent: 0 });
        const fd = new FormData();
        fd.append('file', file);
        try {
          const result = await checkpointApi.uploadFile(teamId, checkpointNumber, fd, {
            onUploadProgress: ({ loaded, total }) => {
              const progressTotal = total || file.size;
              const percent = progressTotal > 0
                ? Math.min(100, Math.round((loaded / progressTotal) * 100))
                : 0;
              setUploadState({
                fileName: file.name,
                index: index + 1,
                total: validFiles.length,
                percent,
              });
            },
          });
          uploadedCount += 1;
          const version = result?.data?.versionNumber;
          toast.success(`"${file.name}" uploaded${version ? ` as Version ${version}` : ''}!`);
        } catch (error) {
          toast.error(checkpointUploadFailureMessage(error, file.name));
        }
      }

      if (uploadedCount > 0) {
        await onUploaded?.();
      }
    } finally {
      uploadInProgressRef.current = false;
      setUploading(false);
      setUploadState(null);
      if (fileRef.current) fileRef.current.value = '';
    }
  };

  return (
    <div
      onClick={() => !uploading && fileRef.current?.click()}
      onDragOver={(e) => { e.preventDefault(); setDragging(true); }}
      onDragLeave={() => setDragging(false)}
      onDrop={(e) => {
        e.preventDefault();
        setDragging(false);
        if (!uploading) processFiles(e.dataTransfer.files);
      }}
      className={`
        relative border-2 border-dashed flex flex-col items-center justify-center gap-2 cursor-pointer select-none
        transition-all duration-200 text-center
        ${isLarge ? 'rounded-2xl min-h-[200px] p-8 gap-3' : 'rounded-xl min-h-[128px] p-5'}
        ${dragging
          ? 'border-orange-500 bg-orange-50 shadow-inner'
          : 'border-slate-200 bg-white hover:border-orange-400/70 hover:bg-orange-50/30 hover:shadow-sm'}
        ${uploading ? 'pointer-events-none opacity-60' : ''}
      `}
    >
      <input
        ref={fileRef}
        type="file"
        multiple
        accept=".pdf,.docx,.pptx"
        className="hidden"
        onChange={(e) => processFiles(e.target.files)}
      />

      {uploading ? (
        <>
          <Loader2 className="w-7 h-7 text-orange-500 animate-spin" />
          <p className="max-w-full truncate text-xs font-semibold text-slate-600">
            {uploadState?.percent === 100 ? 'Processing' : 'Uploading'} {uploadState?.index}/{uploadState?.total}: {uploadState?.fileName}
          </p>
          <div className="h-1.5 w-full max-w-64 overflow-hidden rounded-full bg-slate-200" role="progressbar" aria-label="File upload progress" aria-valuemin="0" aria-valuemax="100" aria-valuenow={uploadState?.percent ?? 0}>
            <div
              className="h-full rounded-full bg-orange-500 transition-[width] duration-200"
              style={{ width: `${uploadState?.percent ?? 0}%` }}
            />
          </div>
          <p className="text-[10px] text-slate-400">
            {uploadState?.percent === 100 ? 'Saving and validating file…' : `${uploadState?.percent ?? 0}%`}
          </p>
        </>
      ) : (
        <>
          <div className={`rounded-2xl bg-orange-50 flex items-center justify-center ${isLarge ? 'w-16 h-16' : 'w-12 h-12'}`}>
            <UploadCloud className={`transition-colors ${isLarge ? 'w-9 h-9' : 'w-8 h-8'} ${dragging ? 'text-orange-500' : 'text-orange-400'}`} />
          </div>
          <p className={`font-semibold text-slate-700 ${isLarge ? 'text-sm' : 'text-xs'}`}>
            {dragging ? 'Drop files here' : 'Drag & drop or click to upload'}
          </p>
          <p className={`text-slate-400 ${isLarge ? 'text-xs' : 'text-[10px]'}`}>
            PDF · DOCX · PPTX · Max 15 MB per file
          </p>
        </>
      )}
    </div>
  );
}
