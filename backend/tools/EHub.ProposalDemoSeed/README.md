# Local proposal AI demo seed

Công cụ này thêm sáu proposal lịch sử tổng hợp vào database Development local để kiểm thử retrieval và báo cáo AI. Dữ liệu được đặt trong lớp `AI-DEMO-HISTORY`, không dùng thông tin người dùng thật và không tự tạo analysis job.

Từ thư mục `backend/`:

```powershell
dotnet run --project tools/EHub.ProposalDemoSeed -- --apply
```

Biện pháp an toàn:

- Chỉ chạy khi `ASPNETCORE_ENVIRONMENT=Development` hoặc biến này chưa được đặt.
- Chỉ chấp nhận database host `localhost`, `127.0.0.1` hoặc `::1`.
- Không sử dụng hoặc in API key.
- Kiểm tra migration trước khi ghi dữ liệu.
- Ghi toàn bộ dữ liệu trong một transaction.
- Idempotent: chạy lại không tạo bản sao.

Dữ liệu gồm ba proposal liên quan đến nhà ở sinh viên và ba proposal đối chứng khác chủ đề. Hãy dùng nội dung proposal mục tiêu trong `docs/ai/PROPOSAL_AI_DEMO_DATA.md` để thử nghiệm trên giao diện.
