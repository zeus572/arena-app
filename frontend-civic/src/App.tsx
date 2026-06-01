import { BrowserRouter, Routes, Route } from "react-router-dom";
import { AuthProvider } from "@/auth/AuthContext";

import MagazineLayout from "@/prototypes/magazine/Layout";
import MagazineHome from "@/prototypes/magazine/pages/Home";
import MagazineBriefingDetail from "@/prototypes/magazine/pages/BriefingDetail";
import MagazineThinkDeeper from "@/prototypes/magazine/pages/ThinkDeeper";
import MagazineOnboarding from "@/prototypes/magazine/pages/Onboarding";
import MagazineProfile from "@/prototypes/magazine/pages/Profile";
import MagazineBudget from "@/prototypes/magazine/pages/Budget";
import MagazineReceipt from "@/prototypes/magazine/pages/Receipt";
import MagazineLogin from "@/prototypes/magazine/pages/Login";
import MagazineRegister from "@/prototypes/magazine/pages/Register";
import MagazineQuizzes from "@/prototypes/magazine/pages/Quizzes";
import MagazineConceptDetail from "@/prototypes/magazine/pages/ConceptDetail";
import MagazineTeachers from "@/prototypes/magazine/pages/Teachers";
import MagazineBillTimeline from "@/prototypes/magazine/pages/BillTimeline";
import MagazineCampaignFeed from "@/prototypes/magazine/pages/CampaignFeed";
import MagazineCandidateProfile from "@/prototypes/magazine/pages/CandidateProfile";
import MagazineCandidateSources from "@/prototypes/magazine/pages/CandidateSources";
import MagazinePostDetail from "@/prototypes/magazine/pages/PostDetail";
import MagazineMatchMe from "@/prototypes/magazine/pages/MatchMe";
import MagazineCampaigns from "@/prototypes/magazine/pages/Campaigns";
import MagazineCampaignCreate from "@/prototypes/magazine/pages/CampaignCreate";
import MagazineCampaignDashboard from "@/prototypes/magazine/pages/CampaignDashboard";

function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route path="/" element={<MagazineLayout />}>
            <Route index element={<MagazineHome />} />
            <Route path="briefings/:slug" element={<MagazineBriefingDetail />} />
            <Route path="think-deeper/:slug" element={<MagazineThinkDeeper />} />
            <Route path="onboarding" element={<MagazineOnboarding />} />
            <Route path="profile" element={<MagazineProfile />} />
            <Route path="budget" element={<MagazineBudget />} />
            <Route path="receipt/:id" element={<MagazineReceipt />} />
            <Route path="login" element={<MagazineLogin />} />
            <Route path="register" element={<MagazineRegister />} />
            <Route path="quizzes" element={<MagazineQuizzes />} />
            <Route path="concepts/:slug" element={<MagazineConceptDetail />} />
            <Route path="teachers" element={<MagazineTeachers />} />
            <Route path="timelines/bill" element={<MagazineBillTimeline />} />
            <Route path="candidates" element={<MagazineCampaignFeed />} />
            <Route path="candidates/:slug" element={<MagazineCandidateProfile />} />
            <Route path="candidates/:slug/sources" element={<MagazineCandidateSources />} />
            <Route path="posts/:id" element={<MagazinePostDetail />} />
            <Route path="match" element={<MagazineMatchMe />} />
            <Route path="campaigns" element={<MagazineCampaigns />} />
            <Route path="campaigns/new" element={<MagazineCampaignCreate />} />
            <Route path="campaigns/:id" element={<MagazineCampaignDashboard />} />
          </Route>
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  );
}

export default App;
