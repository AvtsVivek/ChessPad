namespace ChessPad.ViewModels.Tests;

/// <summary>Tests board construction, factory contracts, coordinate ordering, labels, and read-only collections.</summary>
/// <remarks>
/// Each case supplies direct construction delegates and owns its board. Recording delegates
/// verify both arguments and returned object identity without depending on Autofac or WPF.
/// </remarks>
[TestClass]
public sealed class ChessBoardViewModelTests
{
    /// <summary>Verifies that board construction creates exactly 64 fields ordered by row and then column.</summary>
    /// <remarks>Compares the complete coordinate sequence, detecting missing, duplicated, or reordered fields.</remarks>
    [TestMethod]
    public void Constructor_CreatesAll64CoordinatesInRowMajorOrder()
    {
        var board = CreateBoard();
        var expected = Enumerable.Range(0, 8)
            .SelectMany(row => Enumerable.Range(0, 8).Select(column => (row, column)))
            .ToArray();
        var actual = board.Fields.Select(field => (field.Row, field.Column)).ToArray();

        Assert.HasCount(64, board.Fields);
        CollectionAssert.AreEqual(expected, actual);
    }

    /// <summary>Verifies that every coordinate is passed to the injected field factory in row-major order.</summary>
    /// <remarks>Records factory calls and checks reference identity so the board cannot replace the returned fields.</remarks>
    [TestMethod]
    public void Constructor_UsesFieldFactoryForEveryCoordinateAndPreservesReturnedInstances()
    {
        var calls = new List<(int Row, int Column)>();
        var createdFields = new List<FieldViewModel>();

        var board = new ChessBoardViewModel(
            (row, column) =>
            {
                calls.Add((row, column));
                var field = new FieldViewModel(row, column);
                createdFields.Add(field);
                return field;
            },
            (index, label) => new RowColumnLabelViewModel(index, label));

        var expected = Enumerable.Range(0, 8)
            .SelectMany(row => Enumerable.Range(0, 8).Select(column => (row, column)))
            .ToArray();
        CollectionAssert.AreEqual(expected, calls.ToArray());
        Assert.HasCount(64, board.Fields);
        for (int index = 0; index < createdFields.Count; index++)
        {
            Assert.AreSame(createdFields[index], board.Fields[index]);
        }
    }

    /// <summary>Verifies descending rank text, ascending file text, and zero-based indices for both label collections.</summary>
    /// <remarks>Compares all eight entries on each axis rather than relying only on endpoint labels.</remarks>
    [TestMethod]
    public void Constructor_CreatesRankLabelsFromEightToOneAndFileLabelsFromAToH()
    {
        var board = CreateBoard();

        CollectionAssert.AreEqual(
            new[] { "8", "7", "6", "5", "4", "3", "2", "1" },
            board.RowLabels.Select(label => label.Label).ToArray());
        CollectionAssert.AreEqual(
            new[] { "A", "B", "C", "D", "E", "F", "G", "H" },
            board.ColumnLabels.Select(label => label.Label).ToArray());
        CollectionAssert.AreEqual(
            new[] { 0, 1, 2, 3, 4, 5, 6, 7 },
            board.RowLabels.Select(label => label.Index).ToArray());
        CollectionAssert.AreEqual(
            new[] { 0, 1, 2, 3, 4, 5, 6, 7 },
            board.ColumnLabels.Select(label => label.Index).ToArray());
    }

    /// <summary>Verifies the 16 rank/file factory arguments and preservation of each returned label instance.</summary>
    /// <remarks>Matches each expected index/text pair to its recorded factory result and checks reference identity.</remarks>
    [TestMethod]
    public void Constructor_UsesLabelFactoryAndPreservesReturnedInstances()
    {
        var calls = new List<(int Index, string Label, RowColumnLabelViewModel Instance)>();
        var expectedRanks = new[] { "8", "7", "6", "5", "4", "3", "2", "1" };
        var expectedFiles = new[] { "A", "B", "C", "D", "E", "F", "G", "H" };

        var board = new ChessBoardViewModel(
            (row, column) => new FieldViewModel(row, column),
            (index, label) =>
            {
                var instance = new RowColumnLabelViewModel(index, label);
                calls.Add((index, label, instance));
                return instance;
            });

        Assert.HasCount(16, calls);
        Assert.HasCount(8, board.RowLabels);
        Assert.HasCount(8, board.ColumnLabels);
        for (int index = 0; index < 8; index++)
        {
            var rankCall = calls.Single(call => call.Index == index && call.Label == expectedRanks[index]);
            var fileCall = calls.Single(call => call.Index == index && call.Label == expectedFiles[index]);
            Assert.AreSame(rankCall.Instance, board.RowLabels[index]);
            Assert.AreSame(fileCall.Instance, board.ColumnLabels[index]);
        }
    }

    /// <summary>Verifies that fields and both label collections reject mutation through their list interfaces.</summary>
    /// <remarks>Checks read-only flags, rejected add/replace/clear operations, and collection state after those attempts.</remarks>
    [TestMethod]
    public void Collections_RejectMutationThroughTheirListInterfaces()
    {
        var board = CreateBoard();
        var fields = Assert.IsInstanceOfType<IList<FieldViewModel>>(board.Fields);
        var ranks = Assert.IsInstanceOfType<IList<RowColumnLabelViewModel>>(board.RowLabels);
        var files = Assert.IsInstanceOfType<IList<RowColumnLabelViewModel>>(board.ColumnLabels);

        Assert.IsTrue(fields.IsReadOnly);
        Assert.IsTrue(ranks.IsReadOnly);
        Assert.IsTrue(files.IsReadOnly);
        Assert.ThrowsExactly<NotSupportedException>(() => fields.Add(new FieldViewModel(0, 0)));
        Assert.ThrowsExactly<NotSupportedException>(() => ranks[0] = new RowColumnLabelViewModel(0, "1"));
        Assert.ThrowsExactly<NotSupportedException>(() => files.Clear());
        Assert.HasCount(64, board.Fields);
        Assert.AreEqual("8", board.RowLabels[0].Label);
        Assert.HasCount(8, board.ColumnLabels);
    }

    /// <summary>Creates a board with direct construction delegates instead of an Autofac container.</summary>
    /// <returns>A new board with independently created fields and labels for an isolated unit test.</returns>
    private static ChessBoardViewModel CreateBoard() => new(
        (row, column) => new FieldViewModel(row, column),
        (index, label) => new RowColumnLabelViewModel(index, label));
}
