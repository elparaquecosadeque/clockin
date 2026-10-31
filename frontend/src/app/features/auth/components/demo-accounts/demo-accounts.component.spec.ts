import { TestBed } from '@angular/core/testing';
import { DemoAccount } from '../../models/demo-accounts.model';
import { DemoAccountsComponent } from './demo-accounts.component';

describe('DemoAccountsComponent', () => {
  const eve: DemoAccount = {
    email: 'eve@clockin.demo',
    name: 'Eve',
    role: 'Employee',
    team: 'Warehouse',
    scenario: 'ClockedIn',
  };

  it('shows the shared password and hands back the account picked', async () => {
    const fixture = TestBed.createComponent(DemoAccountsComponent);
    fixture.componentRef.setInput('demo', { password: 'clockin-demo', accounts: [eve] });
    const picked: DemoAccount[] = [];
    fixture.componentInstance.picked.subscribe((account) => picked.push(account));
    await fixture.whenStable();
    const element: HTMLElement = fixture.nativeElement;

    expect(element.textContent).toContain('clockin-demo');
    const button = element.querySelector('button')!;
    const text = (selector: string) =>
      button.querySelector(selector)?.textContent?.replace(/\s+/g, ' ').trim();
    expect([text('.name'), text('.role'), text('.scenario')]).toEqual([
      'Eve',
      'Employee · Warehouse',
      'Clocked in',
    ]);
    button.click();

    expect(picked).toEqual([eve]);
  });
});
