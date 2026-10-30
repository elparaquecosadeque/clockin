import { DurationPipe } from './duration.pipe';

describe('DurationPipe', () => {
  const pipe = new DurationPipe();

  it('shows hours and minutes', () => {
    expect(pipe.transform(2 * 3600 + 5 * 60 + 59)).toBe('2:05');
  });

  it('shows seconds when asked', () => {
    expect(pipe.transform(3600 + 7, true)).toBe('1:00:07');
  });

  it('keeps counting past a day and never goes negative', () => {
    expect(pipe.transform(40 * 3600)).toBe('40:00');
    expect(pipe.transform(-5)).toBe('0:00');
  });
});
