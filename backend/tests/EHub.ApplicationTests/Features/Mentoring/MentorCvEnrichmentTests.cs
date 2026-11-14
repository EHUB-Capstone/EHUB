using EHub.Application.Features.Mentoring.ExtractCv;
using EHub.Domain.Entities;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Mentoring;

public sealed class MentorCvEnrichmentTests
{
    [Fact]
    public void ExplicitProfessionalTechnologyAndDomainsHaveExactSafeSourceQuotes()
    {
        const string source = """
            Personal information
            PRIVATE_MARKER
            Education
            Finance and software engineering at Demo Academy
            Work experience
            Built React and C# services for a FinTech product.
            Led e-commerce logistics projects using PostgreSQL.
            Technical skills
            Docker and machine learning; liên hệ fictional@example.test
            Family
            PRIVATE_FAMILY_MARKER
            """;
        var result = MentorCvProfileParser.Parse(source).Value;
        result.TechnologySkills.Should().Contain(["React", "C#", "PostgreSQL", "Docker", "Machine learning"]);
        result.StartupDomains.Should().Contain(["FinTech", "E-commerce", "Logistics"]);
        result.Expertise.Should().NotContain("Finance").And.NotContain("Software engineering");
        result.Evidence.Should().OnlyContain(item => source.Contains(item.Quote) && item.Quote.Length <= 320);
        result.Evidence.Should().NotContain(item => item.Quote.Contains("PRIVATE") || item.Quote.Contains("@"));
    }

    [Theory]
    [InlineData("No React or FinTech experience; Built PostgreSQL services", "PostgreSQL")]
    [InlineData("Không có kinh nghiệm Python hay EdTech. Phát triển ứng dụng bằng Flutter", "Flutter")]
    [InlineData("Without Java or healthcare experience, but developed Rust services", "Rust")]
    public void DeniedSkillsAndDomainsAreNotPromotedFromNearbyPositiveNarratives(string narrative, string affirmative)
    {
        var result = MentorCvProfileParser.Parse("Work experience\n" + narrative).Value;
        result.TechnologySkills.Should().Equal(affirmative);
        result.StartupDomains.Should().BeEmpty();
        result.Evidence.Should().OnlyContain(item => !item.Quote.Contains("No ") && !item.Quote.Contains("Không") && !item.Quote.Contains("Without"));
    }

    [Fact]
    public void DegreesAloneNeverBecomeTechnologyOrStartupExperience()
    {
        var result = MentorCvProfileParser.Parse("Education\nFinance, React, PostgreSQL, FinTech and healthcare at Demo Academy").Value;
        result.Bio.Should().Contain("React");
        result.Expertise.Should().BeEmpty();
        result.TechnologySkills.Should().BeEmpty();
        result.StartupDomains.Should().BeEmpty();
        result.Evidence.Should().BeEmpty();
    }

    [Fact]
    public void AGeneralSummaryHeadingCannotTurnADegreeIntoProfessionalTechnologyExperience()
    {
        var result = MentorCvProfileParser.Parse("Professional summary\nMaster in FinTech using React\nPh.D. in machine learning").Value;
        result.Bio.Should().Contain("Master");
        result.TechnologySkills.Should().BeEmpty();
        result.StartupDomains.Should().BeEmpty();
    }

    [Fact]
    public void SelectedProjectsAndProfessionalRolesRetainTheirNarrative()
    {
        var result = MentorCvProfileParser.Parse("Selected projects\nFinTech platform built using Node.js\nProfessional roles: Technical lead\nDự án e-commerce dùng React").Value;
        result.Experience.Should().Contain("Technical lead").And.Contain("Dự án e-commerce dùng React");
        result.StartupDomains.Should().Contain(["FinTech", "E-commerce"]);
        result.TechnologySkills.Should().Contain(["Node.js", "React"]);
    }

    [Fact]
    public void TechnologyTokenBoundariesDoNotTurnJavaScriptIntoJavaOrCppIntoCSharp()
    {
        var result = MentorCvProfileParser.Parse("Technical skills\nJavaScript and C++").Value;
        result.TechnologySkills.Should().BeEquivalentTo(["JavaScript", "C++"]);
    }

    [Fact]
    public void ApplyingTheSameCvIsIdempotentAndPreservesManualValuesAndEvidence()
    {
        var manual = new MentorExperience { Kind = "Technology", Area = "React", Years = 7, Level = "Senior", Notes = "Manually reviewed delivery" };
        var profile = new MentorProfile
        {
            Bio = "Reviewed background", Experience = "Reviewed work", Expertise = ["Manual expertise"],
            StartupDomains = ["Manual industry"], TechnologySkills = ["React", "Manual technology"]
        };
        profile.Experiences.Add(manual);
        var extracted = MentorCvProfileParser.Parse("Education\nDemo Academy\nWork experience\nBuilt React and PostgreSQL for a FinTech project").Value;

        MentorCvProfileEnrichment.Apply(profile, extracted);
        var once = (profile.Bio, profile.Experience);
        MentorCvProfileEnrichment.Apply(profile, extracted);

        (profile.Bio, profile.Experience).Should().Be(once);
        profile.Bio.Should().StartWith("Reviewed background").And.Contain("Demo Academy");
        profile.Experience.Should().StartWith("Reviewed work").And.Contain("Built React");
        profile.TechnologySkills.Should().Equal("React", "Manual technology", "PostgreSQL");
        profile.StartupDomains.Should().Equal("Manual industry", "FinTech");
        profile.Experiences.Should().HaveCount(3);
        profile.Experiences.Should().ContainSingle(item => item.Area == "React").Which.Should().BeSameAs(manual);
        manual.Notes.Should().Be("Manually reviewed delivery");
        manual.Years.Should().Be(7);
        profile.Experiences.Where(item => item != manual).Should().OnlyContain(item =>
            item.Notes == "CV: Built React and PostgreSQL for a FinTech project");
    }

    [Fact]
    public void FactsPastTheNarrativeLimitRemainAvailableAsSourceBackedExperience()
    {
        const string relevant = "Built Python services for an EdTech project";
        var extracted = MentorCvProfileParser.Parse("Work experience\n" + new string('x', 4500) + "\n" + relevant).Value;
        extracted.Experience.Should().HaveLength(4000).And.NotContain(relevant);
        var profile = new MentorProfile();

        MentorCvProfileEnrichment.Apply(profile, extracted);

        profile.TechnologySkills.Should().Contain("Python");
        profile.StartupDomains.Should().Contain("EdTech");
        profile.Experiences.Should().Contain(item => item.Area == "Python" && item.Notes == "CV: " + relevant);
        profile.Experiences.Should().Contain(item => item.Area == "EdTech" && item.Notes == "CV: " + relevant);
    }

    [Fact]
    public void FullManualFieldsKeepTheirLimitsWithoutLosingExistingValues()
    {
        var profile = new MentorProfile
        {
            Bio = new string('a', 2000), Experience = new string('b', 4000),
            TechnologySkills = Enumerable.Range(1, 20).Select(index => "Reviewed technology " + index).ToArray()
        };
        for (var index = 0; index < 40; index++)
            profile.Experiences.Add(new MentorExperience { Kind = "Technology", Area = "Manual " + index, Notes = "Reviewed" });
        var extracted = MentorCvProfileParser.Parse("Education\nDemo Academy\nWork experience\nReact for FinTech").Value;

        MentorCvProfileEnrichment.Apply(profile, extracted);

        profile.Bio.Should().Be(new string('a', 2000));
        profile.Experience.Should().Be(new string('b', 4000));
        profile.TechnologySkills.Should().HaveCount(20).And.NotContain("React");
        profile.Experiences.Should().HaveCount(40).And.OnlyContain(item => item.Notes == "Reviewed");
    }
}
