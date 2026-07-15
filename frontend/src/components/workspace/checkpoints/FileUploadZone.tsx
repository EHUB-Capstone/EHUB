// @ts-nocheck
// src/components/workspace/checkpoints/FileUploadZone.jsx
import { useRef, useState } from 'react';
import { UploadCloud, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { checkpointApi } from '../../../api/checkpointApi';

const ALLOWED_EXT  = ['.pdf', '.docx', '.pptx'];
const MAX_SIZE     = 15 * 1024 * 1024; // 15 MB (MongoDB document limit)

export default function FileUploadZone({ teamId, checkpointNumber, onUploaded, variant = 'default' }) {
  const isLarge = variant === 'large';
  const [dragging,   setDragging]   = useState(false);
  const [uploading,  setUploading]  = useState(false);
  const fileRef = useRef(null);

  const processFiles = async (rawFiles) => {
    const files = Array.from(rawFiles);
    if (!files.length) return;

    setUploading(true);
    try {
      for (const file of files) {
        // Client-side size check
        if (file.size > MAX_SIZE) {
          toast.error(`"${file.name}" exceeds the 15 MB limit.`);
          continue;
        }
        // Client-side extension check
        const ext = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
        if (!ALLOWED_EXT.includes(ext)) {
          toast.error(`"${file.name}" — unsupported format. Use ${ALLOWED_EXT.join(', ')}.`);
          continue;
        }

        const fd = new FormData();
        fd.append('file', file);
        await checkpointApi.uploadFile(teamId, checkpointNumber, fd);
        toast.success(`"${file.name}" uploaded!`);
      }
      onUploaded?.();
    } catch (e) {
      toast.error(e?.response?.data?.error || e.message || 'Upload failed.');
    } finally {
      setUploading(false);
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
        processFiles(e.dataTransfer.files);
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
          <p className="text-xs font-semibold text-slate-600">Uploading…</p>
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
