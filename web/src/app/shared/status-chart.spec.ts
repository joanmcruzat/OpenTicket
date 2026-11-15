import { describe, expect, it } from 'vitest';
import { STATUS_COLORS, toSlices } from './status-chart';

describe('toSlices', () => {
  it('labels, colours and sizes every status, skipping a path for zero counts', () => {
    const slices = toSlices([
      { status: 'Backlog', count: 4 },
      { status: 'InProgress', count: 2 },
      { status: 'WontDo', count: 0 },
    ]);

    expect(slices.map((s) => s.label)).toEqual(['Backlog', 'In Progress', "Won't Do"]);
    expect(slices.map((s) => s.share)).toEqual([67, 33, 0]);
    expect(slices.map((s) => s.color)).toEqual([STATUS_COLORS.Backlog, STATUS_COLORS.InProgress, STATUS_COLORS.WontDo]);
    expect(slices[0]!.path).toMatch(/^M .+ Z$/);
    expect(slices[2]!.path).toBe('');
  });

  it('draws a single status as a full ring (two half arcs)', () => {
    const [only] = toSlices([{ status: 'Done', count: 3 }]);

    expect(only!.share).toBe(100);
    expect(only!.path.match(/Z/g)).toHaveLength(2);
  });

  it('handles an empty board without dividing by zero', () => {
    expect(toSlices([{ status: 'ToDo', count: 0 }])[0]).toMatchObject({ share: 0, path: '' });
  });

  it('gives each status a fixed colour, whatever is filtered out', () => {
    const [done] = toSlices([{ status: 'Done', count: 1 }]);

    expect(done!.color).toBe(STATUS_COLORS.Done);
  });
});
