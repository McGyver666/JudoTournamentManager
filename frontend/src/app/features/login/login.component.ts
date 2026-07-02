import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthStateService } from '../../core/auth-state.service';
import { TranslatePipe } from '../../core/translate.pipe';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, TranslatePipe],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css',
})
export class LoginComponent {
  private readonly auth = inject(AuthStateService);
  private readonly router = inject(Router);

  protected readonly userName = signal('');
  protected readonly password = signal('');

  protected readonly loading = this.auth.loading;
  protected readonly error = this.auth.error;

  protected async submit(): Promise<void> {
    const ok = await this.auth.login(this.userName().trim(), this.password());
    if (ok) {
      await this.router.navigateByUrl('/tournaments');
    }
  }
}
