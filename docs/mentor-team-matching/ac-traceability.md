# Gán mentor cho nhóm theo kỳ: đối chiếu AC với hiện thực và test

Tài liệu nghiệm thu cho tính năng gán mentor cho nhóm theo kỳ (REQ-01 đến REQ-09). Mỗi acceptance criteria (AC) được ghi cùng nơi hiện thực và test chứng minh. Mục "Kiểm tra thủ công" liệt kê những AC chỉ xem được trên giao diện.

Ký hiệu thư mục test: `AppTests` = `backend/tests/EHub.ApplicationTests/Features/Admin/Mentors`, `IntTests` = `backend/tests/EHub.IntegrationTests/Admin`, `FeTests` = `frontend/tests`.

## 1. Bảng đối chiếu

### AC-01: Mentor tham gia kỳ

| AC | Hiện thực | Test |
|---|---|---|
| 01.1 Chọn mentor active theo kỳ | Danh sách mentor tổng + `SemesterStaffAssignments` (có sẵn, không đổi) | `MentorImportIntegrationTests.AdminImport_ShouldCreateBothMentorTypes_AndAddThemToSelectedSemester` |
| 01.2 Nhóm EXE201 kết thúc được lưu trữ, không kế thừa | Hoàn thành lớp kết thúc phân công (có sẵn); `MentorRetentionPlanner` không bao giờ kế thừa nhóm EXE201 | `MentorAssignmentExportIntegrationTests.CompletingAnExe101Class_...`, `MentorRetentionPlannerTests.Plan_NeverCarriesAnExe201TeamForward` |
| 01.3 Mentor không active thì không được dùng | Truy vấn ứng viên chỉ lấy mentor active trong kỳ; `MentorManualEditPlanner` từ chối | `MentorImportIntegrationTests.RandomAllocation_...` (mentor ngoài kỳ không bao giờ được chọn), `MentorManualEditPlannerTests.Plan_RejectsAMentorWhoIsNotActiveThisSemester` |

### AC-02: Giữ mentor cho nhóm tiếp tục

| AC | Hiện thực | Test |
|---|---|---|
| 02.1 Đề xuất giữ mentor, mọi nhóm tiếp tục của mentor | `MentorRetentionPlanner`, `MentorRetentionDataLoader` | `MentorRetentionPlannerTests.Plan_KeepsBothMentorsForAContinuingTeamAndIgnoresTheTeamName`, `Plan_KeepsOnlyTheContinuingTeamsOfAMentorWithSeveralTeams`, `MentorImportIntegrationTests.Allocation_ShouldRetainMentors...` |
| 02.2 Đổi tên nhóm vẫn nhận ra | Nhận diện theo `PreviousTeamId`, không theo tên | `Plan_KeepsBothMentorsForAContinuingTeamAndIgnoresTheTeamName` |
| 02.3 Trùng tên không có liên kết thì không giữ | Như trên | `Plan_DoesNotMatchTeamsWithoutALineageLinkEvenWhenTheNamesMatch` |
| 02.4 Mentor cũ không active: không đề xuất, hiển thị mờ | `Skipped` kèm lý do `MentorNotActiveInSemester`; tab "Needs attention" hiển thị mờ | `Plan_ShowsAMentorWhoIsNotActiveThisSemesterAsSkippedAndKeepsTheSlotFree`, integration (nhóm `continuingTwo`) |
| 02.5 Hai vị trí xét độc lập | Planner xét từng vị trí | `Plan_ShowsAMentorWhoIsNotActiveThisSemester...` (giữ doanh nghiệp, bỏ giảng viên) |
| 02.6 Admin chỉnh tay đề xuất | `edits` trong request, ưu tiên cao hơn giữ mentor | `MentorImportIntegrationTests.Allocation_ShouldRetainMentors...` (chỉnh tay đè mentor được giữ) |
| 02.7 Nhóm không đủ điều kiện kế thừa: cảnh báo, không đề xuất | `NoContinuedTeam`; cảnh báo cho giảng viên đã có ở bước kế thừa nhóm | `Plan_KeepsOnlyTheContinuingTeamsOfAMentorWithSeveralTeams`, integration (mentor `dropped`) |

### AC-03: Nhóm cũ đã hoàn thành

| AC | Hiện thực | Test |
|---|---|---|
| 03.1 Mentor active nhưng nhóm cũ không tiếp tục: chưa phân công | `mentorLoads.totalBefore = 0`, nhãn "Unassigned" | `CompletingAnExe101Class_...` (`TotalBefore` bằng 0), `MentorLoadSummaryBuilderTests.Build_ListsMentorsWithoutAnyTeamAsUnassigned` |
| 03.2 Mentor nhiều nhóm: chỉ giữ nhóm tiếp tục | Planner | `Plan_KeepsOnlyTheContinuingTeamsOfAMentorWithSeveralTeams` |
| 03.3 Nhóm EXE201 lưu trữ, lịch sử còn | Phân công `Ended` (không xóa), `EndedAt` bằng thời điểm hoàn thành lớp | `CompletingAnExe101Class_...` |

### AC-04: Không tự ghi đè, xung đột

| AC | Hiện thực | Test |
|---|---|---|
| 04.1 Giữ nguyên mentor hiện tại | Engine chỉ điền chỗ trống; chỉ mục duy nhất theo nhóm và vị trí | `MentorAllocationEngineTests.Allocate_NeverTouchesSlotsThatAlreadyHaveAMentor`, `Plan_NeverOverwritesADifferentMentorAlreadyInTheSlot` |
| 04.2 Cảnh báo xung đột | `conflicts` (`SlotOccupied`), `existingAssignments` | `ManualEdits_ShouldNotOverwriteAnOccupiedSlot...`, `Plan_DoesNotOverwriteAnOccupiedSlotWithoutAnExplicitReplace` |
| 04.3 Thay thế: kết thúc cũ và tạo mới cùng một lần lưu | Giao dịch tuần tự hóa trong `CommitAllocationAsync` | `ReplacingAMentor_ShouldEndTheOldAssignmentAndCreateTheNewOneTogether` |
| 04.4 Lỗi thì giữ nguyên | Cùng giao dịch; kiểm tra phân công cần thay còn nguyên trước khi lưu | `ReplacingAMentor_ShouldBeRejectedWhenTheCurrentMentorChangedAfterThePreview` (không lưu gì khi 409). Chưa có test cố tình gây lỗi giữa giao dịch. |

### AC-05, AC-06, AC-07: Chia mentor

| AC | Hiện thực | Test |
|---|---|---|
| 05.1 Chỉ đúng loại mentor | `MentorAllocationEngine` lọc theo loại | `Allocate_FillsBothSlotsWithMatchingMentorTypeOnly`, `Allocate_RandomStrategy_StillRespectsTheHardRules`, `Plan_RejectsAMentorOfTheWrongType` |
| 05.2 Nhóm đã đủ thì không đưa vào danh sách chia | Engine bỏ qua vị trí đã có | `Allocate_ProducesNoWarningWhenNothingIsMissingEvenWithoutMentors` |
| 06.1 Có Random và Balanced, Balanced mặc định | Server mặc định `Balanced`; giao diện chọn sẵn Balanced | `BalancedAllocation_ShouldFillBothSlotsAndKeepLoadsWithinOne` (strategy mặc định). Giao diện: kiểm tra thủ công. |
| 06.2 Random | `RandomMentorSelector` | `Allocate_RandomStrategy_*`, `RandomAllocation_ShouldSaveWhatCanBeAssignedAndReportTheMissingMentorType` |
| 06.3 Balanced ưu tiên ít nhóm | `BalancedMentorSelector` | `Allocate_KeepsLoadsWithinOneOfEachOtherPerMentorType` (107 nhóm), `Allocate_MatchesLegacyInlineAlgorithmForTheSameSeed` |
| 06.4 Tải gồm nhóm giữ lại và đã có | Engine nhận cả phân công đã giữ, đã sửa tay | `Allocate_CountsExistingAssignmentsAsMentorLoad`, integration (mentor giữ 2 nhóm không được chọn thêm) |
| 06.5 Hòa tải chọn ngẫu nhiên, tái lập được | Cùng seed cho cùng kết quả | `Allocate_IsDeterministicForTheSameSeed` |
| 07.1 Một mentor nhiều nhóm, không chặn cứng | Không có giới hạn trong engine | `FullSemester_ShouldBeAssignedBalancedSavedAndExportedWithConsistentTotals` (107 nhóm, 37 mentor) |
| 07.2 Mỗi nhóm tối đa một mentor mỗi loại | Chỉ mục duy nhất + planner | `Plan_AllowsOnlyOneEditPerTeamSlot`, scale test (không trùng) |
| 07.3 Không tạo bản ghi trùng khi xác nhận hai lần | Phiên một lần dùng, khóa xử lý | `Commit_ShouldCreateEachAssignmentOnceWhenConfirmedTwiceAtTheSameTime` |

### AC-08: Xem trước, sửa tay, xác nhận

| AC | Hiện thực | Test |
|---|---|---|
| 08.1 Chưa xác nhận thì dữ liệu chính thức không đổi | Xem trước chỉ ghi phiên | `RandomAllocation_...` (không có phân công nào sau xem trước), `Allocation_ShouldRetainMentors...` |
| 08.2 Đủ nội dung xem trước | `assignments`, `unfilled`, `skipped`, `mentorLoads`, `conflicts`, `existingAssignments` | `RandomAllocation_...`, `Allocation_ShouldRetainMentors...`, `mentorAllocationPreview.test.ts` |
| 08.3 Sửa tay trong bản xem trước | `edits` + `MentorManualEditPlanner` | `MentorManualEditPlannerTests`, `ManualEdits_...`, `ReplacingAMentor_...` |
| 08.4 Xác nhận lưu đúng như xem trước | `CommitAllocationAsync` | `Allocation_ShouldRetainMentors...`, scale test (lưu 214 phân công) |
| 08.5 Hủy thì không lưu | Chỉ đóng modal; phiên tự hết hạn | Kiểm tra thủ công |
| 08.6 Dữ liệu đổi sau xem trước thì từ chối | Kiểm tra từng vị trí và tải mentor lúc xác nhận | `ReplacingAMentor_ShouldBeRejectedWhenTheCurrentMentorChangedAfterThePreview`, `Commit_ShouldBeRejectedWhenAMentorLoadChangedAfterThePreview` |
| 08.7 Khóa bấm xác nhận hai lần | Khóa phía server; nút có trạng thái đang lưu | `Commit_ShouldCreateEachAssignmentOnceWhenConfirmedTwiceAtTheSameTime` |
| 08.8 Chỉ Admin (401, 403) | `[Authorize(Policy = AdminOnly)]` ở controller | `AllocationEndpoints_ShouldBeDeniedWithoutAnAdministrator`, `Preview_ShouldReturn401/403_*` |

### AC-09: Xuất Excel

| AC | Hiện thực | Test |
|---|---|---|
| 09.1 Một file cho cả kỳ, đủ lớp, 3 sheet | `MentorAssignmentExportHandler`, `GET /api/admin/mentors/assignments/export` | `Export_ShouldReturnThreeSheetsWithTheMentorsInEffectAndASummary` (2 lớp EXE201), scale test |
| 09.2 Sheet EXE101 và EXE201 theo mẫu Hình 3 | `ClassRosterExportWorkbookBuilder.WriteSheet` | `MentorAssignmentExportWorkbookBuilderTests.Build_AlwaysProducesTheThreeSheetsInOrder_EvenWithoutData`, `Build_WritesTheMentorsOnTheFirstRowOfEachTeam...` |
| 09.3 Thiếu mentor ghi "Chưa phân công" | Nhãn `UnassignedLabel` (chỉ ở file xuất theo kỳ) | `Build_WritesTheMentorsOnTheFirstRowOfEachTeamAndMarksMissingOnesAsUnassigned`, integration |
| 09.4 Sheet Tổng hợp khớp hai sheet đầu | Đếm đúng các nhóm có trên hai sheet | `Build_SummaryCountsTeamsPerMentorAndSubjectAndListsIdleMentors`, `Build_SummaryTotalsOfBothMentorTypesMatchWhenEveryTeamHasBothMentors`, scale test (28/79/107 ở cả hai bảng) |
| 09.5 Chỉ dữ liệu của kỳ, bỏ phân công đã kết thúc giữa kỳ | Phân công "đang có hiệu lực": còn Active hoặc kết thúc cùng lúc hoàn thành lớp | `Export_ShouldReturnThreeSheets...` (mentor bị thay không xuất hiện), `CompletingAnExe101Class_...` |
| 09.6 Chỉ Admin | Controller `AdminOnly` | `Export_ShouldBeDeniedWithoutAnAdministrator` (401, 403, 404 kỳ không có) |

## 2. Kiểm tra thủ công trên giao diện

Các mục này chưa có kiểm thử tự động cho giao diện. Cần xem trên `/admin/subjects`, mục "Mentor import & assignment":

1. Balanced được chọn sẵn; đổi sang Random thì dòng giải thích đổi theo (AC-06.1).
2. Cửa sổ xem trước hiển thị ba tab, bảng cuộn ngang được trên màn hình hẹp, và hoạt động ở chế độ tối.
3. Mentor kỳ trước không giữ được hiển thị mờ kèm lý do ở tab "Needs attention" (AC-02.4).
4. Chỉnh một ô mentor: vị trí trống chọn được mentor hoặc để trống; vị trí đã có mentor yêu cầu lý do từ 3 ký tự và hiện "Replaces …" (AC-04.2, 04.3).
5. Có mentor bị thay thì xác nhận hiện hộp thoại "Replace current mentors?" trước khi lưu.
6. Đóng cửa sổ (Cancel) thì không có gì được lưu (AC-08.5).
7. Nút "Export assignments" tải đúng file `.xlsx` ba sheet.

## 3. Mặc định đã chọn, cần cô xác nhận

| Nội dung | Mặc định |
|---|---|
| Balanced tính tải | Tổng số nhóm hai môn, riêng từng loại mentor |
| Ô thiếu mentor trong file xuất theo kỳ | Ghi "Chưa phân công" (xuất lớp lẻ giữ ô trống như cũ) |
| Kỳ chưa có lớp | File chỉ có dòng tiêu đề, không báo lỗi |
| Lớp ở trạng thái Draft | Vẫn được xuất, giống chức năng xuất hiện có |
| Mentor bị thay thế | Phân công cũ chuyển `Ended` kèm lý do; mentor mất quyền vào nhóm; thông báo dùng sự kiện `Team.MentorAssignmentChanged.v1` có sẵn |
| Mentor vừa bị thay | Vẫn có thể được Balanced gán cho nhóm khác trong cùng bản xem trước |
| Quyền | Xem trước, sửa tay hàng loạt, xác nhận và xuất file chỉ Admin; giảng viên chỉnh từng nhóm ở màn lớp như trước |

## 4. Hạn chế đã biết

1. **Mở lại lớp đã hoàn thành không khôi phục phân công mentor** (hành vi có sẵn của chức năng hoàn thành lớp). Lớp mở lại không còn mentor, và việc giữ mentor cho kỳ sau không thấy mentor của lớp đó cho tới khi gán lại.
2. Test chưa mô phỏng lỗi cơ sở dữ liệu giữa giao dịch lưu. Tính toàn vẹn dựa trên giao dịch tuần tự hóa và các test từ chối khi dữ liệu đổi.
3. Chưa có mock cho các endpoint chia mentor; giao diện này cần backend thật.

## 5. Triển khai và hoàn tác

- **Không có thay đổi schema, không có migration.** Phiên xem trước lưu JSON trong bảng sẵn có; các trường mới đều tùy chọn nên phiên cũ vẫn đọc được.
- **Hoàn tác:** revert code. Phân công đã tạo có thể kết thúc bằng chức năng "kết thúc phân công" có sẵn (cần lý do). Phân công bị thay thế chỉ chuyển sang `Ended`, không bị xóa nên khôi phục bằng gán lại.
- Backend và frontend phát hành cùng nhau: giao diện mới gửi `strategy` và `edits`, backend cũ sẽ bỏ qua chúng.

## 6. Lệnh kiểm tra đã chạy

Backend (từ `backend/`): `dotnet build EHub.slnx`, `dotnet test EHub.slnx --filter "FullyQualifiedName!~IntegrationTests"`, `dotnet test tests/EHub.IntegrationTests/EHub.IntegrationTests.csproj`.
Frontend (từ `frontend/`): `npm run lint`, `npm run type-check`, `npm test`, `npm run build`.
