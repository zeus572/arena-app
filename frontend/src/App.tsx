import { BrowserRouter, Routes, Route } from "react-router-dom";
import { AuthProvider } from "@/contexts/AuthContext";
import { Navbar } from "@/components/navbar";
import { ProtectedRoute } from "@/components/ProtectedRoute";
import Feed from "@/pages/Feed";
import DebateView from "@/pages/DebateView";
import StartArgument from "@/pages/StartArgument";
import Agents from "@/pages/Agents";
import Topics from "@/pages/Topics";
import Login from "@/pages/Login";
import Register from "@/pages/Register";
import AuthCallback from "@/pages/AuthCallback";
import Profile from "@/pages/Profile";
import Sources from "@/pages/Sources";

function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Navbar />
        <Routes>
          <Route path="/" element={<Feed />} />
          <Route path="/start" element={<ProtectedRoute requiredPlan="Premium"><StartArgument /></ProtectedRoute>} />
          <Route path="/debates/:id" element={<DebateView />} />
          <Route path="/agents" element={<Agents />} />
          <Route path="/topics" element={<Topics />} />
          <Route path="/login" element={<Login />} />
          <Route path="/register" element={<Register />} />
          <Route path="/auth/callback" element={<AuthCallback />} />
          <Route path="/profile" element={<ProtectedRoute><Profile /></ProtectedRoute>} />
          <Route path="/sources" element={<Sources />} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  );
}

export default App;
