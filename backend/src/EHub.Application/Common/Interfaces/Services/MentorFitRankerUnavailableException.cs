namespace EHub.Application.Common.Interfaces.Services;

public sealed class MentorFitRankerUnavailableException : Exception
{
    public MentorFitFailure Failure { get; } = MentorFitFailure.Unknown;
    public string UserMessage => Failure switch
    {
        MentorFitFailure.Configuration => "Dịch vụ AI chưa được cấu hình hợp lệ trên máy chủ. Vui lòng kiểm tra cấu hình AI.",
        MentorFitFailure.InputLimit => "Dữ liệu cần chấm vượt giới hạn xử lý. Vui lòng chọn phạm vi lớp nhỏ hơn.",
        MentorFitFailure.InvalidInput => "Dữ liệu dự án hoặc hồ sơ mentor chưa hợp lệ để AI chấm điểm. Vui lòng kiểm tra lại dữ liệu.",
        MentorFitFailure.Authentication => "API key của dịch vụ AI không hợp lệ hoặc đã hết hiệu lực. Vui lòng kiểm tra key trên máy chủ.",
        MentorFitFailure.Permission => "Dịch vụ AI từ chối quyền truy cập. Vui lòng kiểm tra quyền của API key và project trên máy chủ.",
        MentorFitFailure.Quota => "Dịch vụ AI đã chạm hạn mức sử dụng. Vui lòng đợi hạn mức được khôi phục rồi thử lại.",
        MentorFitFailure.ModelUnavailable => "Các model AI đã cấu hình hiện không khả dụng. Vui lòng kiểm tra cấu hình model trên máy chủ.",
        MentorFitFailure.ServiceUnavailable => "Dịch vụ AI đang tạm thời quá tải. Vui lòng thử lại sau.",
        MentorFitFailure.RequestRejected => "Dịch vụ AI từ chối yêu cầu chấm điểm. Vui lòng kiểm tra model và cấu hình máy chủ.",
        MentorFitFailure.Timeout => "AI chưa phản hồi trong thời gian cho phép. Vui lòng thử lại với phạm vi lớp nhỏ hơn.",
        MentorFitFailure.Network => "Máy chủ chưa kết nối được dịch vụ AI. Vui lòng kiểm tra kết nối mạng của VPS.",
        MentorFitFailure.InvalidResponse => "AI trả về kết quả chưa hợp lệ. Vui lòng thử lại để chấm lại lô chưa hoàn tất.",
        _ => "Dịch vụ AI phân bổ mentor tạm thời chưa sẵn sàng. Vui lòng thử lại."
    };

    public MentorFitRankerUnavailableException() : base("Mentor ranking model is unavailable.") { }

    public MentorFitRankerUnavailableException(MentorFitFailure failure) : this() => Failure = failure;

    /// <summary>API errors use only the safe, fixed UserMessage, never the inner exception.</summary>
    public MentorFitRankerUnavailableException(Exception innerException)
        : base("Mentor ranking model is unavailable.", innerException) { }
}
