import { RoleLabelPipe } from './role-label.pipe';

describe('RoleLabelPipe', () => {
  it('names every role', () => {
    const pipe = new RoleLabelPipe();
    expect(pipe.transform('Employee')).toBe('Employee');
    expect(pipe.transform('Supervisor')).toBe('Supervisor');
    expect(pipe.transform('Admin')).toBe('Admin');
  });
});
