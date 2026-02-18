# Specification: Relationship Line Arrows for SE Dependencies Diagram

## Overview

Add directional arrowheads to the relationship lines in the xmSQL ERD (Storage Engine Dependencies) diagram to visually indicate the direction of outer joins. This provides immediate visual feedback about data flow direction without needing to read the join type label.

## Current Behavior

- Relationship lines are rendered as **dashed bezier curves** (`StrokeDashArray="4 2"`) between tables.
- Lines have no directional indicators (no arrowheads).
- Join type is displayed as a text label at the midpoint of the line (e.g., "LEFT OUTER", "INNER", "RIGHT OUTER", "FULL OUTER").
- Lines connect via the closest edge (top, bottom, left, or right) of each table, calculated in `ErdRelationshipViewModel.UpdatePath()`.

## Proposed Behavior

Add arrowheads to the **end** of relationship lines based on the `XmSqlJoinType`:

| Join Type | Arrow Placement | Visual Meaning |
|---|---|---|
| `LeftOuterJoin` | Arrow on the **To-table** end (pointing at the To-table) | The "left" (From) table drives the join; arrow points to the table being joined to |
| `RightOuterJoin` | Arrow on the **From-table** end (pointing at the From-table) | The "right" (To) table drives the join; arrow points back to the From table |
| `InnerJoin` | **No arrow** | Both sides are equally filtered; no directional indicator needed |
| `FullOuterJoin` | **Arrows on both ends** | Both sides preserve unmatched rows; arrows on both ends |
| `Unknown` | **No arrow** | Insufficient information to determine direction |

## Files to Modify

### 1. `DaxStudio.UI/ViewModels/XmSqlErdViewModel.cs` — `ErdRelationshipViewModel` class (line ~4715)

#### New Properties

Add the following computed properties to `ErdRelationshipViewModel`:

```csharp
/// <summary>
/// Whether to show an arrow at the end point (To-table side) of the relationship line.
/// </summary>
public bool ShowArrowAtEnd => JoinType == XmSqlJoinType.LeftOuterJoin 
                           || JoinType == XmSqlJoinType.FullOuterJoin;

/// <summary>
/// Whether to show an arrow at the start point (From-table side) of the relationship line.
/// </summary>
public bool ShowArrowAtStart => JoinType == XmSqlJoinType.RightOuterJoin 
                             || JoinType == XmSqlJoinType.FullOuterJoin;
```

#### New Computed Geometry Properties

Add properties that compute arrowhead polygon points based on the line's start/end coordinates and edge direction. The arrowheads need to orient correctly regardless of whether the line exits from the top, bottom, left, or right edge of a table.

```csharp
/// <summary>
/// Gets the arrowhead path data for the end (To-table) side.
/// Returns a small triangle polygon pointing in the direction of the line's arrival at the end point.
/// </summary>
public string EndArrowPathData { get; }

/// <summary>
/// Gets the arrowhead path data for the start (From-table) side.
/// Returns a small triangle polygon pointing in the direction of the line's arrival at the start point.
/// </summary>
public string StartArrowPathData { get; }
```

**Arrow geometry calculation:**

The arrow is a filled triangle. Arrow size constants:
- `ArrowLength = 12` (pixels along the line direction)
- `ArrowWidth = 8` (pixels perpendicular to the line, i.e., half-width = 4)

The arrow tip sits at the endpoint. The base of the triangle is `ArrowLength` pixels back along the approach direction. The approach direction is derived from the bezier curve's tangent at the endpoint, which can be approximated from the last control point to the endpoint.

For the **end arrow** (at `EndX, EndY`):
- The tangent direction comes from the last bezier control point toward `(EndX, EndY)`.
- For a horizontal connection: the last control point is at `(midX, EndY)`, so the tangent is purely horizontal.
- For a vertical connection: the last control point is at `(EndX, midY)`, so the tangent is purely vertical.

For the **start arrow** (at `StartX, StartY`):
- The tangent direction comes from the first bezier control point toward `(StartX, StartY)` (reversed direction).
- Same horizontal/vertical logic as above, but mirrored.

The calculation must also account for the `ParallelOffset` applied to the line path.

#### Property Change Notifications

When `StartX`, `StartY`, `EndX`, `EndY`, or `ParallelOffset` change, also raise `NotifyOfPropertyChange` for:
- `StartArrowPathData`
- `EndArrowPathData`

When the relationship is first created or `UpdatePath()` is called, the arrow properties are automatically kept in sync since they derive from the same coordinates.

### 2. `DaxStudio.UI/Views/XmSqlErdView.xaml` — Relationship Lines ItemTemplate (line ~1180)

#### Add Arrowhead Paths

Inside the relationship line `DataTemplate` (the `<Grid>` that contains the dashed `<Path>`), add two additional `Path` elements for the arrowheads — one for each end. These are **filled** (not stroked) triangles:

```xml
<!-- Arrow at End (To-table side) - for LEFT OUTER and FULL OUTER joins -->
<Path Data="{Binding EndArrowPathData}"
      Fill="{DynamicResource Theme.Brush.Accent}"
      Visibility="{Binding ShowArrowAtEnd, Converter={StaticResource BoolToCollapsedConverter}}">
    <Path.Style>
        <Style TargetType="Path">
            <Setter Property="Opacity" Value="1"/>
            <Style.Triggers>
                <DataTrigger Binding="{Binding IsHighlighted}" Value="True">
                    <Setter Property="Fill" Value="#FF0078D4"/>
                </DataTrigger>
                <DataTrigger Binding="{Binding IsDimmed}" Value="True">
                    <Setter Property="Opacity" Value="0.3"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </Path.Style>
</Path>

<!-- Arrow at Start (From-table side) - for RIGHT OUTER and FULL OUTER joins -->
<Path Data="{Binding StartArrowPathData}"
      Fill="{DynamicResource Theme.Brush.Accent}"
      Visibility="{Binding ShowArrowAtStart, Converter={StaticResource BoolToCollapsedConverter}}">
    <Path.Style>
        <Style TargetType="Path">
            <Setter Property="Opacity" Value="1"/>
            <Style.Triggers>
                <DataTrigger Binding="{Binding IsHighlighted}" Value="True">
                    <Setter Property="Fill" Value="#FF0078D4"/>
                </DataTrigger>
                <DataTrigger Binding="{Binding IsDimmed}" Value="True">
                    <Setter Property="Opacity" Value="0.3"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </Path.Style>
</Path>
```

**Placement:** These `Path` elements should be added **after** the existing relationship line `Path` within the same `Grid`, so the arrowheads render on top of the dashed line.

**Note:** The arrowheads use `Fill` (not `Stroke`) so they appear as solid filled triangles. They use the same accent color as the relationship line and respond to the same `IsHighlighted` / `IsDimmed` visual states.

## Arrow Geometry Detail

### Approach Direction by Edge Type

| Edge at endpoint | Approach direction (dx, dy) | Arrow points toward |
|---|---|---|
| `Left` | `(-1, 0)` — arriving from the right | ← Left |
| `Right` | `(+1, 0)` — arriving from the left | → Right |
| `Top` | `(0, -1)` — arriving from below | ↑ Up |
| `Bottom` | `(0, +1)` — arriving from above | ↓ Down |

### Triangle Construction

Given endpoint `(px, py)` and unit approach direction `(dx, dy)`:

```
Tip:   (px, py)
Left:  (px - dx * ArrowLength - dy * ArrowHalfWidth,  py - dy * ArrowLength + dx * ArrowHalfWidth)
Right: (px - dx * ArrowLength + dy * ArrowHalfWidth,  py - dy * ArrowLength - dx * ArrowHalfWidth)
```

Where `ArrowHalfWidth = ArrowWidth / 2 = 4`.

The path data is: `M tip L left L right Z`

### Example

For a **Left Outer Join** where the To-table is to the **right** of the From-table:
- The line enters the To-table from its **Left** edge.
- `_endEdge = EdgeType.Left`, so approach direction is `(-1, 0)`.
- Tip at `(EndX, EndY)`.
- Triangle points leftward (into the To-table).

## Visual Design

```
  ┌──────────┐                         ┌──────────┐
  │ FromTable│--- - - - - - - - - -▶──│ ToTable  │   LEFT OUTER JOIN
  └──────────┘                         └──────────┘

  ┌──────────┐                         ┌──────────┐
  │ FromTable│──◀- - - - - - - - - ---│ ToTable  │   RIGHT OUTER JOIN
  └──────────┘                         └──────────┘

  ┌──────────┐                         ┌──────────┐
  │ FromTable│--- - - - - - - - - - --│ ToTable  │   INNER JOIN (no arrow)
  └──────────┘                         └──────────┘

  ┌──────────┐                         ┌──────────┐
  │ FromTable│──◀- - - - - - - - -▶──│ ToTable  │   FULL OUTER JOIN
  └──────────┘                         └──────────┘
```

## Testing Considerations

1. **All join types** — Verify arrows appear/hide correctly for each of the 5 `XmSqlJoinType` values.
2. **All edge orientations** — Verify arrows orient correctly when lines connect via left, right, top, and bottom edges (tables positioned in various arrangements).
3. **Parallel offset** — Verify arrows render correctly when `ParallelOffset` is non-zero (multiple relationships between the same pair of tables).
4. **Highlighting / dimming** — Verify arrowheads follow the same highlight/dim behavior as the line itself.
5. **Query filtering** — Verify arrowheads are hidden when `IsQueryFiltered` is true (inherited from the parent Grid's Visibility binding).
6. **Mini-map** — Arrowheads at this scale will be negligible; no special handling needed.
7. **Export to image / clipboard** — Arrowheads should be captured as part of the canvas render since they're standard WPF Path elements.

## Scope / Out of Scope

**In scope:**
- Arrowheads on relationship lines based on join type
- Correct orientation for all 4 edge types
- Matching visual states (highlighted, dimmed, query-filtered)

**Out of scope:**
- Changes to the relationship label text
- Changes to cardinality symbols
- Any tooltip changes
- Inner join directional indicators (intentionally omitted)
- User-configurable arrow size or style
