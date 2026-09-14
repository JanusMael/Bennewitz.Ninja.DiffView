using System.Text;

namespace Bennewitz.Ninja.DiffView.Tests.Spike;

// Phase 1 spike, throwaway. A hand-rolled alignment so the spike can pad two documents without
// the Core model that Phase 3 builds.

internal abstract record AlignmentOp(int Count);

/// <summary>Rows present on both sides.</summary>
internal sealed record Same(int Count) : AlignmentOp(Count);

/// <summary>Rows present on the left only; the right gets padding.</summary>
internal sealed record LeftOnly(int Count) : AlignmentOp(Count);

/// <summary>Rows present on the right only; the left gets padding.</summary>
internal sealed record RightOnly(int Count) : AlignmentOp(Count);

/// <summary>
/// Two documents, the padding each side carries by line number, the line pairs that share a
/// row, and the row count both sides must reach once padded.
/// </summary>
internal sealed class AlignmentFixture
{
    private AlignmentFixture(string leftText, string rightText,
                             IReadOnlyDictionary<int, PaddingSpec> leftPadding, IReadOnlyDictionary<int, PaddingSpec> rightPadding,
                             IReadOnlyList<(int Left, int Right)> alignedPairs, int rowCount)
    {
        LeftText = leftText;
        RightText = rightText;
        LeftPadding = leftPadding;
        RightPadding = rightPadding;
        AlignedPairs = alignedPairs;
        RowCount = rowCount;
    }

    public string LeftText { get; }

    public string RightText { get; }

    public IReadOnlyDictionary<int, PaddingSpec> LeftPadding { get; }

    public IReadOnlyDictionary<int, PaddingSpec> RightPadding { get; }

    public IReadOnlyList<(int Left, int Right)> AlignedPairs { get; }

    public int RowCount { get; }

    public static AlignmentFixture Build(params AlignmentOp[] ops)
    {
        Side left = new("left");
        Side right = new("right");
        List<(int Left, int Right)> pairs = [];
        int rows = 0;

        foreach (AlignmentOp op in ops)
        {
            for (int i = 0; i < op.Count; i++)
            {
                rows++;
                switch (op)
                {
                    case Same:
                        pairs.Add((left.AddLine(), right.AddLine()));
                        break;
                    case LeftOnly:
                        left.AddLine();
                        right.PendingPadding++;
                        break;
                    case RightOnly:
                        right.AddLine();
                        left.PendingPadding++;
                        break;
                    default:
                        throw new ArgumentException($"Unknown alignment op {op.GetType().Name}.", nameof(ops));
                }
            }
        }

        left.CloseTrailingPadding();
        right.CloseTrailingPadding();

        return new AlignmentFixture(left.Text, right.Text, left.Padding, right.Padding, pairs, rows);
    }

    private sealed class Side(string name)
    {
        private readonly StringBuilder _text = new();
        private int _lineCount;

        public Dictionary<int, PaddingSpec> Padding { get; } = [];

        public int PendingPadding { get; set; }

        public string Text => _text.ToString();

        public int AddLine()
        {
            if (_lineCount > 0)
            {
                _text.Append('\n');
            }

            _lineCount++;
            _text.Append(name).Append(" line ").Append(_lineCount);
            if (PendingPadding > 0)
            {
                Padding[_lineCount] = new PaddingSpec(PendingPadding, 0);
                PendingPadding = 0;
            }

            return _lineCount;
        }

        public void CloseTrailingPadding()
        {
            if (PendingPadding == 0)
            {
                return;
            }

            if (_lineCount == 0)
            {
                throw new InvalidOperationException("A side with no lines cannot carry trailing padding in this fixture.");
            }

            PaddingSpec existing = Padding.GetValueOrDefault(_lineCount);
            Padding[_lineCount] = existing with { Below = PendingPadding };
            PendingPadding = 0;
        }
    }
}
