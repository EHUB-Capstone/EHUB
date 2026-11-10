using EHub.Application.Features.Workspaces.CheckpointSummaries;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointSummaries;

public sealed class CheckpointTextDiffTests
{
    private const string Before =
        "Nhóm khảo sát 150 sinh viên tại TP.HCM về thói quen dùng túi nilon.\n" +
        "Đối thủ trực tiếp là túi vải in sẵn trên Shopee giá 25.000 đồng.\n" +
        "Kế hoạch gọi vốn dự kiến 200 triệu đồng trong quý 3.\n" +
        "Trang 3";

    [Fact]
    public void Compare_SameText_HasNoChanges_AndShortLinesAreIgnored()
    {
        var diff = CheckpointTextDiff.Compare(Before, Before.Replace("Trang 3", "Trang 4"));

        diff.IsEmpty.Should().BeTrue();
        diff.Unchanged.Should().Be(3);
    }

    [Fact]
    public void Compare_RewordedSentenceIsPairedWithItsOldForm_AndANewNumberIsFlagged()
    {
        var after = Before.Replace("khảo sát 150 sinh viên", "khảo sát 320 sinh viên");

        var diff = CheckpointTextDiff.Compare(Before, after);

        diff.Added.Should().BeEmpty();
        diff.Removed.Should().BeEmpty();
        var change = diff.Changed.Should().ContainSingle().Subject;
        change.Before.Text.Should().Contain("150");
        change.After.Text.Should().Contain("320");
        change.FiguresChanged.Should().BeTrue();
        diff.Unchanged.Should().Be(2);
    }

    [Fact]
    public void Compare_RewordingWithoutANumberChange_IsNotFlaggedAsAFigureChange()
    {
        var after = Before.Replace("Đối thủ trực tiếp là túi vải in sẵn trên Shopee", "Đối thủ trực tiếp là túi vải in sẵn bán trên Shopee");

        var change = CheckpointTextDiff.Compare(Before, after).Changed.Should().ContainSingle().Subject;

        change.FiguresChanged.Should().BeFalse();
    }

    [Fact]
    public void Compare_ListsWhatWasAddedAndWhatWasRemoved()
    {
        var after = Before.Replace("Kế hoạch gọi vốn dự kiến 200 triệu đồng trong quý 3.\n", string.Empty) +
                    "\nMô hình doanh thu dựa trên gói đăng ký hằng tháng cho cửa hàng nhỏ.";

        var diff = CheckpointTextDiff.Compare(Before, after);

        diff.Removed.Should().ContainSingle().Which.Text.Should().Contain("gọi vốn");
        diff.Added.Should().ContainSingle().Which.Text.Should().Contain("gói đăng ký");
        diff.Changed.Should().BeEmpty();
    }

    [Fact]
    public void Compare_ARepeatedLineIsCountedAsOftenAsItOccurs()
    {
        var twice = "Nhóm khảo sát 150 sinh viên tại TP.HCM.\nNhóm khảo sát 150 sinh viên tại TP.HCM.";
        var once = "Nhóm khảo sát 150 sinh viên tại TP.HCM.";

        var diff = CheckpointTextDiff.Compare(twice, once);

        diff.Unchanged.Should().Be(1);
        diff.Removed.Should().ContainSingle();
    }

    [Fact]
    public void Figures_NormalisesSpacesAndDecimalCommas()
    {
        CheckpointTextDiff.Figures("Tăng 84,3 % và 1.000 người, N = 51").Should().BeEquivalentTo("84.3%", "1.000", "51");
    }
}
