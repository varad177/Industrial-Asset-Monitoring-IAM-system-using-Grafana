import { useState } from "react";
import { useAuthContext } from "../../App";
import styles from "./Auth.module.css";

export default function LoginPage({ onSwitch }) {
  const { login, loading, error } = useAuthContext();
  const [form, setForm] = useState({ emailOrUsername: "", password: "" });
  const [showPass, setShowPass] = useState(false);

  const set = (k) => (e) => setForm((f) => ({ ...f, [k]: e.target.value }));

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      await login(form.emailOrUsername, form.password);
    } catch (_) {}
  };

  return (
    <div className={styles.page}>
      <div className={styles.decorBlob1} />
      <div className={styles.decorBlob2} />

      <div className={styles.card}>
        {/* Logo / Brand */}
        <div className={styles.brand}>
          <div className={styles.logoMark}>
            <svg viewBox="0 0 32 32" fill="none">
              <rect width="32" height="32" rx="8" fill="var(--g-500)" />
              <path d="M8 22 L12 14 L16 18 L20 10 L24 16" stroke="white" strokeWidth="2.5"
                strokeLinecap="round" strokeLinejoin="round" />
              <circle cx="24" cy="16" r="2.5" fill="white" />
            </svg>
          </div>
          <div>
            <h1 className={styles.brandName}>IAM Monitor</h1>
            <p className={styles.brandSub}>Industrial Asset Intelligence</p>
          </div>
        </div>

        <div className={styles.divider} />

        <h2 className={styles.title}>Welcome back</h2>
        <p className={styles.subtitle}>Sign in to your monitoring dashboard</p>

        {error && (
          <div className={styles.errorBanner}>
            <span>⚠</span> {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className={styles.form}>
          <div className={styles.field}>
            <label htmlFor="emailOrUsername">Email or Username</label>
            <div className={styles.inputWrap}>
              <span className={styles.inputIcon}>
                <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.5">
                  <path d="M2.5 6.5l7.5 5 7.5-5M3 5h14a1 1 0 011 1v8a1 1 0 01-1 1H3a1 1 0 01-1-1V6a1 1 0 011-1z"
                    strokeLinecap="round" />
                </svg>
              </span>
              <input
                id="emailOrUsername"
                type="text"
                placeholder="engineer@company.com"
                value={form.emailOrUsername}
                onChange={set("emailOrUsername")}
                autoComplete="username"
                required
              />
            </div>
          </div>

          <div className={styles.field}>
            <label htmlFor="password">Password</label>
            <div className={styles.inputWrap}>
              <span className={styles.inputIcon}>
                <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.5">
                  <rect x="4" y="9" width="12" height="9" rx="1.5" strokeLinecap="round" />
                  <path d="M7 9V6.5a3 3 0 016 0V9" strokeLinecap="round" />
                </svg>
              </span>
              <input
                id="password"
                type={showPass ? "text" : "password"}
                placeholder="••••••••"
                value={form.password}
                onChange={set("password")}
                autoComplete="current-password"
                required
              />
              <button
                type="button"
                className={styles.passToggle}
                onClick={() => setShowPass((v) => !v)}
                aria-label="Toggle password"
              >
                {showPass ? "Hide" : "Show"}
              </button>
            </div>
          </div>

          <button className={styles.btnPrimary} type="submit" disabled={loading}>
            {loading ? (
              <span className={styles.spinner} />
            ) : (
              <>
                <span>Sign In</span>
                <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M4 10h12M10 4l6 6-6 6" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              </>
            )}
          </button>
        </form>

        <p className={styles.switchText}>
          No account?{" "}
          <button className={styles.switchLink} onClick={onSwitch}>
            Create one →
          </button>
        </p>
      </div>
    </div>
  );
}