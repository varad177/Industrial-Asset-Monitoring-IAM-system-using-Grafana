import { useState } from "react";
import { useAuthContext } from "../../App";
import styles from "./Auth.module.css";

export default function RegisterPage({ onSwitch }) {
  const { register, loading, error } = useAuthContext();
  const [form, setForm] = useState({
    username: "", email: "", password: "", confirmPassword: "",
  });
  const [fieldErrors, setFieldErrors] = useState({});

  const set = (k) => (e) => {
    setForm((f) => ({ ...f, [k]: e.target.value }));
    setFieldErrors((fe) => ({ ...fe, [k]: undefined }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      await register(form);
    } catch (err) {
      if (err?.errors) setFieldErrors(err.errors);
    }
  };

  const strength = (p) => {
    let s = 0;
    if (p.length >= 8) s++;
    if (/[A-Z]/.test(p)) s++;
    if (/[0-9]/.test(p)) s++;
    if (/[^A-Za-z0-9]/.test(p)) s++;
    return s;
  };
  const pw = form.password;
  const s = strength(pw);
  const strengthLabel = ["", "Weak", "Fair", "Strong", "Very strong"][s];
  const strengthColor = ["", "var(--red)", "var(--amber)", "var(--g-500)", "var(--g-700)"][s];

  return (
    <div className={styles.page}>
      <div className={styles.decorBlob1} />
      <div className={styles.decorBlob2} />

      <div className={styles.card} style={{ maxWidth: 460 }}>
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

        <h2 className={styles.title}>Create account</h2>
        <p className={styles.subtitle}>Start monitoring your industrial assets</p>

        {error && <div className={styles.errorBanner}><span>⚠</span> {error}</div>}

        <form onSubmit={handleSubmit} className={styles.form}>
          <div className={styles.row2}>
            <div className={styles.field}>
              <label htmlFor="username">Username</label>
              <input id="username" type="text" placeholder="eng_user"
                value={form.username} onChange={set("username")} required />
              {fieldErrors.username && <span className={styles.fieldErr}>{fieldErrors.username[0]}</span>}
            </div>
            <div className={styles.field}>
              <label htmlFor="email">Email</label>
              <input id="email" type="email" placeholder="you@company.com"
                value={form.email} onChange={set("email")} required />
              {fieldErrors.email && <span className={styles.fieldErr}>{fieldErrors.email[0]}</span>}
            </div>
          </div>

          <div className={styles.field}>
            <label htmlFor="reg-password">Password</label>
            <input id="reg-password" type="password" placeholder="Min 8 characters"
              value={form.password} onChange={set("password")} required />
            {pw && (
              <div className={styles.strengthBar}>
                <div className={styles.strengthTrack}>
                  {[1,2,3,4].map(i => (
                    <div key={i} className={styles.strengthSeg}
                      style={{ background: i <= s ? strengthColor : "var(--n-200)" }} />
                  ))}
                </div>
                <span style={{ color: strengthColor, fontSize: 12 }}>{strengthLabel}</span>
              </div>
            )}
          </div>

          <div className={styles.field}>
            <label htmlFor="confirmPassword">Confirm Password</label>
            <input id="confirmPassword" type="password" placeholder="Re-enter password"
              value={form.confirmPassword} onChange={set("confirmPassword")} required />
            {fieldErrors.confirmPassword && (
              <span className={styles.fieldErr}>{fieldErrors.confirmPassword[0]}</span>
            )}
          </div>

          <button className={styles.btnPrimary} type="submit" disabled={loading}>
            {loading ? <span className={styles.spinner} /> : <span>Create Account</span>}
          </button>
        </form>

        <p className={styles.switchText}>
          Already have an account?{" "}
          <button className={styles.switchLink} onClick={onSwitch}>Sign in →</button>
        </p>
      </div>
    </div>
  );
}