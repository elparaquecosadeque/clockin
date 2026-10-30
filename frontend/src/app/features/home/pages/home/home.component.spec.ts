import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { SessionService } from '../../../../core/services/session.service';
import { HomeComponent } from './home.component';

describe('HomeComponent', () => {
  it('greets the user by name', async () => {
    TestBed.configureTestingModule({
      imports: [HomeComponent],
      providers: [{ provide: SessionService, useValue: { user: signal({ name: 'Ada' }) } }],
    });
    const fixture = TestBed.createComponent(HomeComponent);
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelector('h1')?.textContent).toContain(
      'Welcome, Ada',
    );
  });
});
