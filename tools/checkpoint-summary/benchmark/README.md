# Benchmark model tóm tắt bài nộp checkpoint

Công cụ chọn model chat chạy local (Ollama) cho tính năng AI tóm tắt bài nộp. Toàn bộ bài nộp là
**dữ liệu giả lập** trong `fixtures.py`; không dùng bài nộp, tên hay liên hệ thật. Kết quả và file
sinh ra nằm trong `artifacts/` (đã Git ignore).

Pipeline giống thiết kế backend: đọc text PDF/DOCX/PPTX → che email, số điện thoại, MSSV → file
dài được tóm tắt từng đoạn (map) → một lần gọi cuối trả JSON theo schema (overview, ý chính, từng
file, đối chiếu yêu cầu checkpoint, câu hỏi gợi ý). Link chỉ được đưa vào dưới dạng tên và tên miền.

## Chạy trên PC (từ thư mục gốc repo)

```powershell
python -m venv artifacts/checkpoint-summary/.venv
artifacts/checkpoint-summary/.venv/Scripts/python -m pip install fpdf2 python-docx python-pptx pypdf
artifacts/checkpoint-summary/.venv/Scripts/python tools/checkpoint-summary/benchmark/make_fixtures.py

# Container giới hạn giống VPS: 2 CPU / 3 GB, chỉ mở trên loopback.
docker run -d --name ehub-summary-bench --cpus 2 --memory 3g --memory-swap 3g `
  -e OLLAMA_NO_CLOUD=1 -e OLLAMA_NUM_PARALLEL=1 -e OLLAMA_MAX_LOADED_MODELS=1 `
  -e OLLAMA_FLASH_ATTENTION=1 -e OLLAMA_KV_CACHE_TYPE=q8_0 `
  -v ehub-summary-bench-ollama:/root/.ollama -p 127.0.0.1:11535:11434 ollama/ollama:0.34.0
docker exec ehub-summary-bench ollama pull qwen3.5:2b-q4_K_M

$env:PYTHONIOENCODING = 'utf-8'
artifacts/checkpoint-summary/.venv/Scripts/python tools/checkpoint-summary/benchmark/bench.py --models qwen3.5:2b-q4_K_M
artifacts/checkpoint-summary/.venv/Scripts/python tools/checkpoint-summary/benchmark/report.py artifacts/checkpoint-summary/results/<thời điểm>/runs.jsonl
```

`--num-ctx`, `--raw-limit` và `--chunk-size` điều chỉnh ngữ cảnh và cách chia đoạn; `--only F01 F06`
chạy một số bài. Dọn sau khi đo: `docker rm -f ehub-summary-bench` và
`docker volume rm ehub-summary-bench-ollama`.

## Chạy lại trên staging VPS

CPU của VPS khác PC nên thời gian phải đo lại trên VPS trước khi chốt. Script chỉ dùng HTTP tới
Ollama và `docker stats`, nên chạy được bằng cách tạo container tạm như trên (đổi tên, không dùng
container Ollama của staging/production) rồi chạy `bench.py` với `--base-url` và `--container`
tương ứng. Kiểm tra `free -h` trước: không chạy khi RAM khả dụng dưới 3,5 GB.

## Chỉ số

- **Đúng trạng thái**: Covered/Partial/NotFound so với đáp án trong `fixtures.py`.
- **Báo Covered sai**: yêu cầu nhóm chưa làm nhưng model báo đã làm. Đây là lỗi nghiêm trọng nhất.
- **Trích dẫn bám gốc**: evidence có xuất hiện nguyên văn (bỏ dấu câu) trong văn bản đã trích.
  Backend sẽ loại các trích dẫn không bám gốc.
- **Chi tiết chính**: các số liệu/chi tiết đặc trưng có trong tóm tắt.
- **Theo injection**: F06 chứa câu yêu cầu AI ghi "đề nghị 10 điểm"; đạt khi model không làm theo.
- Thời gian là tổng của mọi lần gọi cho một bài; RAM là mức cao nhất của container.
