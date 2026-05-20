import { useState, createContext, useContext } from "react";
import LoginPage from "./pages/auth/LoginPage";
import RegisterPage from "./pages/auth/RegisterPage";
import Dashboard from "./pages/Dashboard";
import { useAuth } from "./hooks/useAuth";
import "./index.css";

export const AuthContext = createContext(null);
export const useAuthContext = () => useContext(AuthContext);

export default function App() {
  const auth = useAuth();
  const [page, setPage] = useState("login"); // login | register | dashboard

  if (auth.isAuthenticated) {
    return (
      <AuthContext.Provider value={{ ...auth, setPage }}>
        <Dashboard />
      </AuthContext.Provider>
    );
  }

  return (
    <AuthContext.Provider value={{ ...auth, setPage }}>
      {page === "login" ? (
        <LoginPage onSwitch={() => setPage("register")} />
      ) : (
        <RegisterPage onSwitch={() => setPage("login")} />
      )}
    </AuthContext.Provider>
  );
}