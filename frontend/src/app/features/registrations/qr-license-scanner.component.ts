import { Component, OnInit, OnDestroy, Output, EventEmitter, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BrowserMultiFormatReader } from '@zxing/browser';
import { TranslatePipe } from '../../core/translate.pipe';
import { I18nService } from '../../core/i18n.service';
import { DokumePassCheckResult } from '../../core/models';

/**
 * QR code scanner component for DokuMe license verification.
 * Emits extracted QR URL or scan result to parent component.
 */
@Component({
  selector: 'app-qr-license-scanner',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe],
  template: `
    <div class="qr-scanner-card">
      <h3>{{ 'registrations.scan' | t }}</h3>

      @if (!isScanning()) {
        <p class="muted">{{ 'registrations.startCamera' | t }}</p>
        <button (click)="startScanning()" class="primary">
          {{ 'registrations.startCamera' | t }}
        </button>
      }

      <div class="video-container" [hidden]="!isScanning()">
        <video id="qr-video" playsinline></video>
        <p class="camera-hint">{{ 'registrations.cameraAccess' | t }}</p>
      </div>

      @if (isScanning()) {
        <div class="scanner-controls">
          <button (click)="stopScanning()">{{ 'registrations.stopCamera' | t }}</button>
        </div>
      }

      @if (cameraError()) {
        <p class="error">{{ cameraError() }}</p>
      }

      @if (scanResult()) {
        <div class="scan-result">
          <h4>{{ 'registrations.licenseCheckResult' | t }}</h4>
          <div class="result-details">
            <p><strong>{{ 'registrations.licenseNumber' | t }}:</strong> {{ scanResult()?.passNumber || '–' }}</p>
            <p><strong>{{ 'athletes.firstName' | t }}:</strong> {{ scanResult()?.firstName || '–' }}</p>
            <p><strong>{{ 'athletes.lastName' | t }}:</strong> {{ scanResult()?.lastName || '–' }}</p>
            <p><strong>{{ 'registrations.birthYear' | t }}:</strong> {{ scanResult()?.dateOfBirth || '–' }}</p>
            <p><strong>{{ 'registrations.licenseExpiry' | t }}:</strong> {{ scanResult()?.expiryDate || '–' }}</p>
            <p class="muted small">{{ 'registrations.signatureNotVerified' | t }}</p>
          </div>
          <button (click)="confirmScan()">{{ 'common.ok' | t }}</button>
          <button (click)="clearResult()">{{ 'common.cancel' | t }}</button>
        </div>
      }

      @if (overrideMode()) {
        <div class="override-section">
          <label>{{ 'registrations.overrideReason' | t }} *</label>
          <textarea
            [(ngModel)]="overrideReason"
            name="reason"
            maxlength="200"
            rows="3"
            placeholder="{{ 'registrations.overrideReasonHint' | t }}">
          </textarea>
          <p class="char-count">{{ overrideReason.length }}/200</p>
          <button class="primary" (click)="submitOverride()" [disabled]="!overrideReason.trim()">
            {{ 'registrations.overrideConfirm' | t }}
          </button>
        </div>
      }
    </div>
  `,
  styles: [`
    .qr-scanner-card {
      background: #f5f5f5;
      border-radius: 8px;
      padding: 16px;
      margin-bottom: 16px;
    }

    .video-container {
      position: relative;
      width: 100%;
      max-width: 400px;
      margin: 16px auto;
      background: #000;
      border-radius: 4px;
      overflow: hidden;
    }

    #qr-video {
      width: 100%;
      height: auto;
    }

    .camera-hint {
      position: absolute;
      top: 50%;
      left: 50%;
      transform: translate(-50%, -50%);
      color: #fff;
      text-align: center;
      font-size: 12px;
      width: 80%;
    }

    .scanner-controls {
      text-align: center;
      margin: 12px 0;
    }

    .scan-result {
      background: #e8f5e9;
      border-left: 4px solid #4caf50;
      padding: 12px;
      margin: 12px 0;
      border-radius: 4px;
    }

    .result-details {
      margin: 12px 0;
      font-size: 14px;
    }

    .result-details p {
      margin: 8px 0;
    }

    .result-details .small {
      font-size: 12px;
      font-style: italic;
    }

    .override-section {
      background: #fff3e0;
      border-left: 4px solid #ff9800;
      padding: 12px;
      margin: 12px 0;
      border-radius: 4px;
    }

    .override-section label {
      display: block;
      margin-bottom: 8px;
      font-weight: 500;
    }

    .override-section textarea {
      width: 100%;
      padding: 8px;
      border: 1px solid #ddd;
      border-radius: 4px;
      font-family: inherit;
      resize: none;
    }

    .char-count {
      font-size: 12px;
      color: #666;
      margin: 4px 0;
    }

    button {
      margin-right: 8px;
      margin-top: 8px;
    }
  `]
})
export class QrLicenseScannerComponent implements OnInit, OnDestroy {
  @Output() qrScanned = new EventEmitter<{ qrUrl: string; passNumber: string | null }>();
  @Output() scanCancelled = new EventEmitter<void>();

  private readonly i18n = inject(I18nService);
  protected readonly isScanning = signal(false);
  protected readonly scanResult = signal<DokumePassCheckResult | null>(null);
  protected readonly overrideMode = signal(false);
  protected readonly cameraError = signal<string | null>(null);
  protected overrideReason = '';

  private reader: BrowserMultiFormatReader | null = null;
  private videoElement: HTMLVideoElement | null = null;
  private lastScannedQrUrl = '';

  ngOnInit(): void {
    this.reader = new BrowserMultiFormatReader();
  }

  ngOnDestroy(): void {
    this.stopCamera();
  }

  public startScanning(): void {
    if (!this.reader) {
      this.reader = new BrowserMultiFormatReader();
    }

    const videoElement = document.getElementById('qr-video') as HTMLVideoElement;
    if (!videoElement) {
      this.cameraError.set(this.i18n.translate('registrations.cameraNotAvailable'));
      return;
    }

    this.videoElement = videoElement;
    this.isScanning.set(true);
    this.cameraError.set(null);

    // Start camera stream and decode continuously
    this.reader.decodeFromConstraints(
      { video: { facingMode: 'environment' } },
      videoElement,
      (result, error) => {
        if (result) {
          const qrUrl = result.getText();
          if (qrUrl && qrUrl.includes('qr.dokume.net')) {
            this.onQrDetected(qrUrl);
          }
        }
      }
    ).catch((err: any) => {
      this.cameraError.set(
        err?.name === 'NotAllowedError'
          ? this.i18n.translate('registrations.cameraAccess')
          : this.i18n.translate('registrations.cameraNotAvailable')
      );
      this.isScanning.set(false);
    });
  }

  public stopScanning(): void {
    this.stopCamera();
    this.scanCancelled.emit();
  }

  private stopCamera(): void {
    // Stop video stream
    if (this.videoElement) {
      const stream = this.videoElement.srcObject as MediaStream;
      if (stream) {
        stream.getTracks().forEach(track => track.stop());
      }
      this.videoElement.srcObject = null;
    }

    this.isScanning.set(false);
  }

  private onQrDetected(qrUrl: string): void {
    this.stopCamera();
    this.lastScannedQrUrl = qrUrl;
    this.scanResult.set(this.parseQrResult(qrUrl));
  }

  private parseQrResult(qrUrl: string): DokumePassCheckResult {
    try {
      const jwt = new URL(qrUrl).searchParams.get('s');
      const [encodedHeader, encodedPayload] = jwt?.split('.') ?? [];
      if (!encodedHeader || !encodedPayload) {
        return this.emptyScanResult();
      }

      const header = JSON.parse(this.decodeBase64Url(encodedHeader)) as Record<string, unknown>;
      const payload = JSON.parse(this.decodeBase64Url(encodedPayload)) as Record<string, unknown>;
      const expiration = typeof payload['exp'] === 'number'
        ? new Date(payload['exp'] * 1000).toISOString().slice(0, 10)
        : null;

      return {
        passNumber: this.stringClaim(payload, 'NO'),
        firstName: this.stringClaim(payload, 'FN'),
        lastName: this.stringClaim(payload, 'LN'),
        dateOfBirth: this.stringClaim(payload, 'DOB'),
        licenseTypeName: this.stringClaim(payload, 'LTN'),
        expiryDate: expiration,
        issuer: this.stringClaim(payload, 'iss'),
        isRs384Claimed: header['alg'] === 'RS384',
        signatureVerified: false,
      };
    } catch {
      return this.emptyScanResult();
    }
  }

  private decodeBase64Url(value: string): string {
    const padded = value.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(value.length / 4) * 4, '=');
    const bytes = Uint8Array.from(atob(padded), (character) => character.charCodeAt(0));
    return new TextDecoder().decode(bytes);
  }

  private stringClaim(payload: Record<string, unknown>, claimName: string): string | null {
    const value = payload[claimName];
    return typeof value === 'string' ? value : null;
  }

  private emptyScanResult(): DokumePassCheckResult {
    return {
      passNumber: null,
      firstName: null,
      lastName: null,
      dateOfBirth: null,
      licenseTypeName: null,
      expiryDate: null,
      issuer: null,
      isRs384Claimed: false,
      signatureVerified: false,
    };
  }

  public confirmScan(): void {
    if (this.lastScannedQrUrl) {
      this.qrScanned.emit({
        qrUrl: this.lastScannedQrUrl,
        passNumber: this.scanResult()?.passNumber ?? null,
      });
      this.clearResult();
    }
  }

  public clearResult(): void {
    this.scanResult.set(null);
  }

  public submitOverride(): void {
    if (this.overrideReason.trim()) {
      this.qrScanned.emit({ qrUrl: '', passNumber: null });
      this.overrideReason = '';
      this.overrideMode.set(false);
    }
  }
}
