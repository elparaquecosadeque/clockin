import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AppComponent } from './app.component';

@Component({ template: '<p>routed</p>' })
class RoutedComponent {}

describe('AppComponent', () => {
  it('renders the routed page', async () => {
    TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [provideRouter([{ path: '**', component: RoutedComponent }])],
    });
    const fixture = TestBed.createComponent(AppComponent);
    await TestBed.inject(Router).navigateByUrl('/');
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('routed');
  });
});
