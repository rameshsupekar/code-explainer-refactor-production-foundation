import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient, HttpClientModule } from '@angular/common/http';

@Component({
  standalone: true,
  selector: 'app-root',
  imports: [CommonModule, HttpClientModule],
  template: `
    <div style="max-width:1100px; margin:0 auto; padding:24px; font-family:Segoe UI, sans-serif;">
      <h2>CodeExplainer Angular (modern)</h2>

      <div style="display:flex; gap:12px; flex-wrap:wrap; margin-bottom:16px;">
        <input [value]="folderPath()" (input)="folderPath.set($any($event.target).value)" placeholder="./src" style="flex:1; min-width:260px; padding:10px 12px;" />
        <button (click)="analyze()" style="padding:10px 16px;">Analyze</button>
      </div>

      <div *ngIf="items().length === 0">No diagrams yet.</div>

      <div *ngFor="let item of items(); trackBy: trackByName" style="border:1px solid #e5e7eb; border-radius:12px; padding:16px; margin-bottom:16px; background:white;">
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
  folderPath = signal('./src');
  items = signal<Array<{ name: string; type: string; content: string }>>([]);

  private http = inject(HttpClient);

  async analyze() {
    try {
      const resp = await this.http.post<{ diagrams?: any[] }>('/api/diagram/analyze', { path: this.folderPath() }).toPromise();
      this.items.set((resp?.diagrams ?? []).map(d => ({ name: d.name, type: d.type, content: d.content })));
    } catch (e) {
      this.items.set([{ name: 'error', type: 'text', content: 'Unable to reach the API at /api/diagram/analyze.' }]);
    }
  }

  trackByName(_: number, item: { name: string }) {
    return item.name;
  }
}
