using System.Net;
using System.Text;
using System.Text.Json;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Mentoring.TrainedRanking;
using EHub.Domain.Entities;
using EHub.Infrastructure;
using EHub.Infrastructure.Mentoring;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.ApplicationTests.Features.Mentoring;

public sealed class MentorSemanticRerankerTests
{
    [Fact]
    public async Task SemanticEvidenceCanRecoverACandidateWhoseWordingHasNoLexicalMatch()
    {
        var mentor = Mentor("Đã tính tiền vào tiền ra và thời gian duy trì hoạt động cho nhóm sáng lập.");
        var service = new StubHttp((_, _) => Reply([(0.72, 0)]));
        var output = await Ranker(service).RerankAsync("Nhóm muốn biết còn duy trì được bao lâu với số tiền đang có.",
            [mentor], Baseline(mentor, evidence: false), default);

        output[mentor.Id].HasEvidence.Should().BeTrue();
        output[mentor.Id].Score.Should().Be(72);
        output[mentor.Id].Reasons.Should().Contain(x => x.Contains(mentor.Experience!.TrimEnd('.')));
        output[mentor.Id].Reasons.Should().Contain(x => x.Contains("kiểm tra chuyên môn"));
    }

    [Fact]
    public async Task AmbiguousHighSimilarityDoesNotInventNewSpecialtyEvidence()
    {
        var first = Mentor("Nghiên cứu và phát triển sản phẩm cho người dùng.");
        var second = Mentor("Quản lý phát triển dịch vụ và các nhóm sản phẩm.");
        var service = new StubHttp((_, _) => Reply([(0.64, 0), (0.62, 0)]));
        var baseline = Baseline(first, false).Concat(Baseline(second, false)).ToDictionary();

        var output = await Ranker(service).RerankAsync("Cần tăng sản lượng vi sinh vật và thiết kế thiết bị lên men.",
            [first, second], baseline, default);

        output.Values.Should().OnlyContain(x => !x.HasEvidence);
    }

    [Fact]
    public async Task EducationAloneCannotBePromotedBySemanticSimilarity()
    {
        var mentor = Mentor("");
        mentor.Bio = "Cử nhân tài chính doanh nghiệp và ngân hàng.";
        var output = await Ranker(new StubHttp((_, _) => Reply([(0.0, -1)]))).RerankAsync(
            "Nhóm cần tính dòng tiền và số vốn cần huy động.", [mentor], Baseline(mentor, false), default);

        output[mentor.Id].HasEvidence.Should().BeFalse();
    }

    [Theory]
    [InlineData("Business", "Nhóm nghiên cứu thị trường giáo dục.", "Không cần tư vấn kinh doanh, chỉ cần nghiên cứu thị trường.")]
    [InlineData("IT", "Đã phát triển ứng dụng giáo dục trên điện thoại.", "Nghiên cứu thị trường giáo dục bằng phỏng vấn phụ huynh.")]
    [InlineData("IT", "Chỉ làm backend. Không có kinh nghiệm AI hoặc computer vision.", "Cần AI và computer vision để phân loại ảnh.")]
    public async Task HighCosineCannotOverrideRoleOrExplicitDenial(string type, string experience, string project)
    {
        var mentor = Mentor(experience, type);
        var output = await Ranker(new StubHttp((_, _) => Reply([(0.9, 0)]))).RerankAsync(
            project, [mentor], Baseline(mentor, false), default);

        output[mentor.Id].HasEvidence.Should().BeFalse();
    }

    [Fact]
    public async Task RequestHasOnlyAnonymousIdsAndAffirmativeProfessionalExcerpts()
    {
        var mentor = Mentor("Marketing cho sản phẩm tiêu dùng. Liên hệ private@example.test. Không có kinh nghiệm gọi vốn.");
        mentor.User = new User { FullName = "PRIVATE_NAME", Email = "private@example.test" };
        mentor.Organization = "PRIVATE_ORGANIZATION";
        mentor.Bio = "PRIVATE_EDUCATION";
        var service = new StubHttp(async (request, ct) =>
        {
            var body = await request.Content!.ReadAsStringAsync(ct);
            body.Should().NotContain("PRIVATE").And.NotContain("example.test").And.NotContain(mentor.Id.ToString());
            using var json = JsonDocument.Parse(body);
            var candidate = json.RootElement.GetProperty("candidates")[0];
            candidate.GetProperty("id").GetString().Should().Be("0");
            candidate.GetProperty("evidence").EnumerateArray().Select(x => x.GetString())
                .Should().NotContain(x => x!.Contains("Không có kinh nghiệm"));
            request.Content.Headers.ContentLength.Should().BeGreaterThan(0);
            return await Reply([(0.7, 0)]);
        });

        await Ranker(service).RerankAsync("Cần marketing để bán sản phẩm.", [mentor], Baseline(mentor, true), default);
        service.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData("{\"schema\":\"wrong\",\"items\":[]}")]
    [InlineData("{\"schema\":\"ehub-mentor-semantic-v1\",\"items\":[null]}")]
    [InlineData("not JSON")]
    public async Task InvalidServiceResponseFailsSafelyInsteadOfFallingBack(string body)
    {
        var mentor = Mentor("Quản lý nghiên cứu thị trường và bán hàng.");
        var service = new StubHttp((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(body, Encoding.UTF8, "application/json") }));

        var act = () => Ranker(service).RerankAsync("Nghiên cứu thị trường.", [mentor], Baseline(mentor, true), default);

        await act.Should().ThrowAsync<MentorFitRankerUnavailableException>();
    }

    [Theory]
    [InlineData(1.5, 0)]
    [InlineData(0.8, 5)]
    [InlineData(0.8, -1)]
    public async Task OutOfRangeOrUnsupportedEvidenceIsRejected(double similarity, int evidenceIndex)
    {
        var mentor = Mentor("Nghiên cứu thị trường và bán hàng.");
        var act = () => Ranker(new StubHttp((_, _) => Reply([(similarity, evidenceIndex)])))
            .RerankAsync("Nghiên cứu thị trường.", [mentor], Baseline(mentor, true), default);
        await act.Should().ThrowAsync<MentorFitRankerUnavailableException>();
    }

    [Fact]
    public async Task ServiceUnavailableCannotBecomeSuccessfulBaselineRecommendations()
    {
        var mentor = Mentor("Marketing và bán hàng.");
        var service = new StubHttp((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var act = () => Ranker(service).RerankAsync("Cần marketing.", [mentor], Baseline(mentor, true), default);
        await act.Should().ThrowAsync<MentorFitRankerUnavailableException>();
    }

    [Fact]
    public async Task EvidenceFromAnotherModelRevisionIsRejected()
    {
        var mentor = Mentor("Marketing và bán hàng.");
        var service = new StubHttp(async (_, _) =>
        {
            var response = await Reply([(0.7, 0)]);
            var body = await response.Content.ReadAsStringAsync();
            response.Content = new StringContent(body.Replace(LocalSemanticMentorReranker.ModelRevision, "other-model"),
                Encoding.UTF8, "application/json");
            return response;
        });
        var act = () => Ranker(service).RerankAsync("Nghiên cứu thị trường.", [mentor], Baseline(mentor, true), default);
        await act.Should().ThrowAsync<MentorFitRankerUnavailableException>();
    }

    [Fact]
    public async Task PromotionMarginIncludesCandidatesInLaterHttpBatches()
    {
        var mentors = Enumerable.Range(0, 33).Select(_ => Mentor("Quản lý nghiên cứu và phát triển sản phẩm.")).ToArray();
        var service = new StubHttp(async (request, ct) =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var count = body.RootElement.GetProperty("candidates").GetArrayLength();
            var items = Enumerable.Range(0, count).Select(index =>
                (count == 1 ? 0.79 : index == 0 ? 0.8 : 0.1, 0)).ToArray();
            return await Reply(items);
        });
        var baseline = mentors.ToDictionary(x => x.Id, _ => new MentorFitRanking(0, [], false));

        var output = await Ranker(service).RerankAsync("Nhóm cần phát triển sản phẩm theo nhu cầu người dùng.",
            mentors, baseline, default);

        service.Calls.Should().Be(2);
        output.Values.Should().OnlyContain(x => !x.HasEvidence);
    }

    [Fact]
    public void VietnamesePronounDoesNotBecomeAnAiRequest()
    {
        MentorEvidenceText.Positive("Cần làm rõ ai sở hữu mẫu tranh số").Should().NotContain(" ai ");
        MentorEvidenceText.Positive("Cần AI để phân loại ảnh").Should().Contain("ai");
    }

    [Theory]
    [InlineData("https://external.example/")]
    [InlineData("http://127.0.0.1:8091/path")]
    [InlineData("http://user:password@localhost:8091/")]
    [InlineData("http://localhost:8091/?query=private")]
    public void ExternalDestinationsAndEmbeddedCredentialsAreRejected(string uri)
    {
        var config = Config(new() { ["Mentoring:MatchingProvider"] = "Hybrid", ["Mentoring:Semantic:BaseUrl"] = uri });
        var act = () => new ServiceCollection().AddInfrastructure(config);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task CancellationStopsBeforeSendingAnyProfessionalData()
    {
        var mentor = Mentor("Marketing và bán hàng.");
        var service = new StubHttp((_, _) => Reply([(0.7, 0)]));
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var act = () => Ranker(service).RerankAsync("Nghiên cứu thị trường.", [mentor], Baseline(mentor, true), source.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
        service.Calls.Should().Be(0);
    }

    [Fact]
    public void MissingRevenueContextIsKeptInProjectButDeniedMentorExperienceIsExcluded()
    {
        MentorSemanticEvidence.ProjectSnippets("Nhóm chưa có doanh thu và muốn biết còn duy trì được bao lâu.")
            .Should().ContainSingle();
        MentorSemanticEvidence.Snippets("Không có kinh nghiệm AI. Đã phụ trách backend C# và PostgreSQL.")
            .Should().ContainSingle().Which.Should().Contain("backend");
    }

    private static MentorProfile Mentor(string experience, string type = "Business") =>
        new() { Experience = experience, MentorType = type };

    private static Dictionary<Guid, MentorFitRanking> Baseline(MentorProfile mentor, bool evidence) =>
        new() { [mentor.Id] = new(evidence ? 80 : 0, ["v2 evidence"], evidence, evidence ? 80.2 : 0) };

    private static IConfiguration Config(Dictionary<string, string?>? values = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(values ?? []).Build();

    private static LocalSemanticMentorReranker Ranker(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:8091/") }, Config());

    private static Task<HttpResponseMessage> Reply((double Similarity, int EvidenceIndex)[] items) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                schema = "ehub-mentor-semantic-v1", model = LocalSemanticMentorReranker.ModelId,
                revision = LocalSemanticMentorReranker.ModelRevision,
                items = items.Select((item, index) => new { id = index.ToString(), similarity = item.Similarity, evidenceIndex = item.EvidenceIndex })
            }), Encoding.UTF8, "application/json")
        });

    private sealed class StubHttp(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; return response(request, cancellationToken); }
    }
}
