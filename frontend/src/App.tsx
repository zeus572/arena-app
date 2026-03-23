import { BrowserRouter, Routes, Route, Link } from "react-router-dom";
import Feed from "./pages/Feed";
import DebateView from "./pages/DebateView";
import Agents from "./pages/Agents";
import StartArgument from "./pages/StartArgument";
import "./App.css";

function App() {
  return (
    <BrowserRouter>
      <nav className="app-nav">
        <Link to="/">Feed</Link>
        <Link to="/start" className="nav-start">Start Argument</Link>
        <Link to="/agents">Agents</Link>
      </nav>
      <main className="app-main">
        <Routes>
          <Route path="/" element={<Feed />} />
          <Route path="/start" element={<StartArgument />} />
          <Route path="/debates/:id" element={<DebateView />} />
          <Route path="/agents" element={<Agents />} />
        </Routes>
      </main>
    </BrowserRouter>
  );
}

export default App;
