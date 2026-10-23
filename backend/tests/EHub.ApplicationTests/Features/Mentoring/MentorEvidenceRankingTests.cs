using EHub.Application.Features.Mentoring.ExtractCv;
using EHub.Domain.Entities;
using EHub.Infrastructure.Mentoring;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace EHub.ApplicationTests.Features.Mentoring;

public sealed class MentorEvidenceRankingTests
{
    private static FileMentorFitRanker Ranker() => new(new ConfigurationBuilder().Build());

    [Theory]
    [InlineData("Không có kinh nghiệm AI, machine learning hoặc computer vision.")]
    [InlineData("Chưa từng làm AI, machine learning hay computer vision.")]
    [InlineData("No experience with AI, machine learning or computer vision.")]
    [InlineData("AI is not required.")]
    public void ExplicitDenialCannotBecomeExpertiseEvenWithAStaleTag(string denial)
    {
        var parsed = MentorCvProfileParser.Parse("Work experience\n" + denial);
        parsed.IsSuccess.Should().BeTrue();
        parsed.Value.Experience.Should().Be(denial);
        parsed.Value.Expertise.Should().NotContain("AI");
        var mentor = new MentorProfile { MentorType = "IT", Experience = denial, Expertise = ["AI"] };

        var result = Ranker().Rank("AI và computer vision", [mentor], default)[mentor.Id];

        result.Score.Should().Be(0);
        result.HasEvidence.Should().BeFalse();
        result.Reasons.Should().NotContain(x => x.StartsWith("Kinh nghiệm có nội dung liên quan"));
    }

    [Theory]
    [InlineData("Không cần AI, computer vision và machine learning; cần backend C#/.NET và PostgreSQL.")]
    [InlineData("No AI or computer vision, but need C# backend and PostgreSQL.")]
    [InlineData("AI nằm ngoài nhu cầu. Cần backend C#/.NET và PostgreSQL.")]
    [InlineData("Không cần AI và cần backend C#/.NET với PostgreSQL.\nCổng AI bán hàng")]
    public void ExclusionsDoNotOverrideTheAffirmativeNeed(string project)
    {
        var dotnet = new MentorProfile { MentorType = "IT", Experience = "Backend C#/.NET và PostgreSQL" };
        var node = new MentorProfile { MentorType = "IT", Experience = "Backend Node.js và PostgreSQL. Không có kinh nghiệm AI.", Expertise = ["AI"] };
        var ai = new MentorProfile { MentorType = "IT", Experience = "AI, machine learning và computer vision" };

        var result = Ranker().Rank(project, [dotnet, node, ai], default);

        result[dotnet.Id].RankingValue.Should().BeGreaterThan(result[node.Id].RankingValue!.Value);
        result[ai.Id].HasEvidence.Should().BeFalse();
        result[node.Id].Reasons.Should().NotContain(x => x.EndsWith(": ai"));
    }

    [Theory]
    [InlineData("Không chỉ marketing mà còn bán hàng.")]
    [InlineData("Không những marketing mà còn sales.")]
    [InlineData("Not only marketing but also sales.")]
    public void NotOnlyConstructionsStillProvidePositiveEvidence(string text)
    {
        var mentor = new MentorProfile { Experience = text };
        Ranker().Rank("Marketing và bán hàng", [mentor], default)[mentor.Id].HasEvidence.Should().BeTrue();
        MentorCvProfileParser.Parse("Work experience\n" + text).Value.Expertise.Should().Contain("Marketing");
    }

    [Fact]
    public void LongestTechnologyPhrasePreventsReactNativeFromBecomingReactWebEvidence()
    {
        var mobile = new MentorProfile { MentorType = "IT", Experience = "React Native, Android và iOS" };
        var web = new MentorProfile { MentorType = "IT", Experience = "React, TypeScript và frontend" };
        var result = Ranker().Rank("Ứng dụng di động bằng React Native", [mobile, web], default);
        result[mobile.Id].HasEvidence.Should().BeTrue();
        result[web.Id].HasEvidence.Should().BeFalse();
    }

    [Fact]
    public void BusinessDescriptionDoesNotInventAnItNeedFromTheSameIndustry()
    {
        var business = new MentorProfile { Experience = "Nghiên cứu thị trường giáo dục" };
        var mobile = new MentorProfile { MentorType = "IT", Experience = "Ứng dụng di động giáo dục Flutter" };
        var result = Ranker().Rank("Nghiên cứu thị trường giáo dục, phỏng vấn phụ huynh", [business, mobile], default);
        result[business.Id].HasEvidence.Should().BeTrue();
        result[mobile.Id].HasEvidence.Should().BeFalse();
    }

    [Fact]
    public void GenericWordOverlapCannotRecommendAnUnsupportedSpecialty()
    {
        var mentor = new MentorProfile { Experience = "Xây quy trình, thực hiện sản phẩm, phối hợp nhóm" };
        var result = Ranker().Rank("Sản phẩm vật liệu sinh học lên men cần mở rộng quy trình", [mentor], default)[mentor.Id];
        result.Score.Should().Be(0);
        result.HasEvidence.Should().BeFalse();
    }

    [Fact]
    public void APartialMobileMatchDoesNotClaimExpertiseInTheRequestedFramework()
    {
        var mentor = new MentorProfile { MentorType = "IT", Experience = "Flutter, Android và iOS" };
        var result = Ranker().Rank("Ứng dụng di động React Native", [mentor], default)[mentor.Id];
        result.HasEvidence.Should().BeTrue();
        result.Reasons.Should().Contain(x => x.StartsWith("Chưa tìm thấy bằng chứng") && x.Contains("React Native"));
        result.Reasons.Should().NotContain(x => x.StartsWith("Kinh nghiệm có nội dung liên quan") && x.Contains("React Native"));
    }

    [Fact]
    public void EducationRemainsInBioWithoutBecomingDeclaredWorkExpertise()
    {
        var result = MentorCvProfileParser.Parse("Education\nTài chính doanh nghiệp\nWork experience\nMarketing và sales").Value;
        result.Bio.Should().Contain("Tài chính");
        result.Expertise.Should().Contain("Marketing").And.NotContain("Finance");
    }
}
