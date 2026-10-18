import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient } from '@angular/common/http';
import { importProvidersFrom } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { FormsModule } from '@angular/forms';
import { AppComponent } from './app/app.component';

// Global error handlers to forward client errors to the API
window.addEventListener('error', (event: ErrorEvent) => {
  fetch('/logs', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      source: 'angular',
      level: 'error',
      type: 'window-error',
      message: event.message,
      filename: event.filename,
      lineno: event.lineno,
      colno: event.colno,
      stack: (event.error && event.error.stack) || null,
      timestamp: new Date().toISOString(),
      userAgent: navigator.userAgent
    })
  }).catch(() => {});
});

window.addEventListener('unhandledrejection', (event: PromiseRejectionEvent) => {
  fetch('/logs', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      source: 'angular',
      level: 'error',
      type: 'unhandledrejection',
      message: event.reason?.message ?? String(event.reason),
      stack: event.reason?.stack ?? null,
      timestamp: new Date().toISOString(),
      userAgent: navigator.userAgent
    })
  }).catch(() => {});
});

bootstrapApplication(AppComponent, {
  providers: [
    provideHttpClient(),
    importProvidersFrom(BrowserModule, FormsModule)
  ]
}).catch(err => console.error(err));
