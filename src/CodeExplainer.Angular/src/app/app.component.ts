import { Component } from '@angular/core';

@Component({
  selector: 'app-root',
  template: `
    <div style="max-width:1100px; margin:0 auto; padding:24px; font-family:Segoe UI, sans-serif;">
      <h2>CodeExplainer Angular</h2>
      <div style="display:flex; gap:12px; flex-wrap:wrap; margin-bottom:16px;">
        <input [(ngModel)]="folderPath" placeholder="./src" style="flex:1; min-width:260px; padding:10px 12px;" />
        <button (click)="analyze()" style="padding:10px 16px;">Analyze</button>
      </div>
      <div *ngIf="!items.length">No diagrams yet.</div>
      <div *ngFor="let item of items" style="border:1px solid #e5e7eb; border-radius:12px; padding:16px; margin-bottom:16px; background:white;">
        <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:12px;">
          <strong>{{ item.name }}</strong>
          <span style="font-size:12px; background:#eef2ff; color:#4338ca; padding:4px 8px; border-radius:999px;">{{ item.type }}</span>
        </div>
        <pre *ngIf="item.type !== 'svg'" style="white-space:pre-wrap; background:#f8fafc; border:1px solid #e2e8f0; padding:12px; border-radius:8px;">{{ item.content }}</pre>
        <iframe *ngIf="item.type === 'svg'" [srcdoc]="item.content" sandbox="allow-scripts" style="width:100%; min-height:260px; border:0;"></iframe>
      </div>
    </div>
  `
})
export class AppComponent {
  folderPath = './src';
  items: Array<{ name: string; type: string; content: string }> = [];

  analyze() {
    fetch('https://localhost:5001/api/diagram/analyze', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ path: this.folderPath })
    })
      .then(r => r.json())
      .then(body => this.items = (body.diagrams ?? []).map((d: any) => ({
        name: d.name,
        type: d.type,
        content: d.content
      })))
      .catch(() => this.items = [{ name: 'error', type: 'text', content: 'Unable to reach the API on https://localhost:5001.' }]);
  }
}
