import { StatusLabelPipe } from './status-label.pipe';

describe('StatusLabelPipe', () => {
  it('names every week status', () => {
    const pipe = new StatusLabelPipe();
    expect(
      ['NotStarted', 'Draft', 'Submitted', 'Approved'].map((s) => pipe.transform(s as never)),
    ).toEqual(['Not started', 'Draft', 'Submitted', 'Approved']);
  });
});
