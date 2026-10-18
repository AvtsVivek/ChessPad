# View models and coordinates

[Documentation index](README.md)

## View-model composition

The runtime object graph is:

```text
MainWindow
`-- DataContext: ChessPadViewModel
	`-- ChessBoard: ChessBoardViewModel
		|-- Fields: IReadOnlyList<FieldViewModel> (64 fields)
		|-- RowLabels: IReadOnlyList<RowColumnLabelViewModel> (8 ranks)
		`-- ColumnLabels: IReadOnlyList<RowColumnLabelViewModel> (8 files)
```

### ChessPadViewModel: application-level composition

`ChessPadViewModel` receives a `ChessBoardViewModel` through its constructor and
exposes that same instance through its get-only `ChessBoard` property:

```csharp
public sealed class ChessPadViewModel
{
	public ChessBoardViewModel ChessBoard { get; }

	public ChessPadViewModel(ChessBoardViewModel chessBoard)
	{
		ChessBoard = chessBoard;
	}
}
```

This is composition: the application view model **has a** chessboard view model;
it does not inherit from one. It neither creates fields nor calculates their
coordinates. Keeping board-specific construction in `ChessBoardViewModel`
leaves the application-level view model focused on composition.

### ChessBoardViewModel: board structure

`ChessBoardViewModel` receives `FieldViewModel.Factory` and
`RowColumnLabelViewModel.Factory` delegates. Its constructor:

1. Creates eight rank labels and eight file labels using the label factory.
2. Exposes those collections through read-only `RowLabels` and `ColumnLabels`.
3. Allocates a list with capacity for 64 fields.
4. Iterates over rows 0 through 7 and columns 0 through 7 within each row.
5. Calls the field factory once for each coordinate pair.
6. Exposes a read-only wrapper through `Fields`.

Its field collection is:

```csharp
public IReadOnlyList<FieldViewModel> Fields { get; }
```

The board owns a flat, row-major collection rather than separate collections of
rows and columns. `AsReadOnly()` prevents callers from modifying the collection
through the exposed wrapper. The board does not subsequently modify the backing
list.

### FieldViewModel: one board location

Each `FieldViewModel` is a sealed record with two positional properties:

```csharp
public sealed record FieldViewModel(int Row, int Column)
{
	// Autofac matches delegate parameters to constructor parameters by name.
	public delegate FieldViewModel Factory(int Row, int Column);

	public bool IsLight => (Row + Column) % 2 == 0;
}
```

`Row` and `Column` are generated init-only record properties. `IsLight` is a
computed property, not independently stored state. Records also provide value
equality: different field instances with the same coordinates compare equal.

The board generates only coordinates in the range 0 through 7. The record itself
has no range validation, so callers outside the board constructor must not assume
that constructing a field automatically validates its coordinates.

## Field coordinates and ordering

The board has 8 rows and 8 columns, for a total of 64 fields.

| Collection index | Row | Column | IsLight |
|---|---|---|---|
| 0 | 0 | 0 | `true` |
| 1 | 0 | 1 | `false` |
| 7 | 0 | 7 | `false` |
| 8 | 1 | 0 | `false` |
| 9 | 1 | 1 | `true` |
| 63 | 7 | 7 | `true` |

The relationship between an index and a coordinate is:

```text
index  = row * 8 + column
row    = index / 8       (integer division)
column = index % 8
```

The view lays out these items from left to right, then top to bottom. Therefore,
`(0, 0)` appears at the top-left and `(7, 7)` at the bottom-right.

### Rank and file labels

Chess rows are called **ranks** and use numbers 1 through 8. Columns are called
**files** and use letters A through H (traditionally lowercase in notation).
The board displays White's perspective: A through H from left to right and
8 through 1 from top to bottom. Thus `(0, 0)` corresponds to A8 and `(7, 7)`
corresponds to H1. Board inversion is not implemented.

`ChessBoardViewModel` exposes two read-only collections of eight
`RowColumnLabelViewModel` records:

- `RowLabels`: `8, 7, 6, 5, 4, 3, 2, 1` for the left and right edges.
- `ColumnLabels`: `A, B, C, D, E, F, G, H` for the top and bottom edges.

Each label has an `Index` (0 through 7, in visual order) and a `Label` string.
These are presentation coordinates, not additional board fields. The same row
collection is rendered on both vertical edges, and the same column collection
on both horizontal edges: 16 label objects produce 32 visible labels.

The board receives `RowColumnLabelViewModel.Factory` through constructor
injection. Its `Index` and `Label` parameters match the record constructor names
exactly, just as the field factory's parameters do. The view models remain
independent of WPF and carry no pixel positions or brushes. See
[delegate factories](dependency-injection.md#parameter-names-matter) for the
Autofac naming requirements.

The parity expression `(Row + Column) % 2 == 0` gives alternating colors across
both axes. The current presentation intentionally starts with a light field at
the top-left. Labels do not change the field ordering or colors.

## Change notifications

The current view models expose constructor-initialized, get-only properties and
a read-only collection. They do not implement `INotifyPropertyChanged`, and the
collection is not observable. This is sufficient for the current board, which
is built before it is bound and does not change afterward.

If future features replace the board, modify the collection, or update field
state after display, add the corresponding property or collection notifications.
Having CommunityToolkit.Mvvm referenced does not automatically make these plain
classes observable.

See [WPF presentation](wpf-presentation.md) for templates and bindings, and
[development](development.md) for verification guidance.

[Documentation index](README.md)
