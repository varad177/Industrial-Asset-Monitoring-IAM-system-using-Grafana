import { useState, createContext, useContext } from "react";
import LoginPage from "./pages/auth/LoginPage";
import RegisterPage from "./pages/auth/RegisterPage";
import Dashboard from "./pages/Dashboard";
import AdminPanel from "./pages/admin/AdminPanel";
import { useAuth } from "./hooks/useAuth";
import "./index.css";

export const AuthContext = createContext(null);
export const useAuthContext = () => useContext(AuthContext);

export default function App() {
  const auth = useAuth();
  const [page, setPage] = useState("login"); // login | register | dashboard | admin

  if (auth.isAuthenticated) {
    const currentPage = (page === "login" || page === "register") ? "dashboard" : page;
    return (
      console.log("Authenticated, rendering page:", currentPage),
      <AuthContext.Provider value={{ ...auth, page: currentPage, setPage }}>
        {currentPage === "admin" && auth.user?.role?.toLowerCase() === "admin" ? <AdminPanel /> : <Dashboard />}
      </AuthContext.Provider> 
    );
  }

  const currentPage = (page !== "login" && page !== "register") ? "login" : page;
  return (
    <AuthContext.Provider value={{ ...auth, page: currentPage, setPage }}>
      {currentPage === "login" ? (
        <LoginPage onSwitch={() => setPage("register")} />
      ) : (
        <RegisterPage onSwitch={() => setPage("login")} />
      )}
    </AuthContext.Provider>
  );
}