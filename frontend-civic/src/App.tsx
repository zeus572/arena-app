import { BrowserRouter, Routes, Route } from "react-router-dom";
import { AuthProvider } from "@/auth/AuthContext";

import MagazineLayout from "@/prototypes/magazine/Layout";
import MagazineHomeIndex from "@/prototypes/magazine/pages/HomeIndex";
import MagazineReadView from "@/prototypes/magazine/pages/MagazineReadView";
import MagazineBriefingDetail from "@/prototypes/magazine/pages/BriefingDetail";
import MagazineTaxApportionment from "@/prototypes/magazine/pages/TaxApportionment";
import MagazineTaxMethodology from "@/prototypes/magazine/pages/TaxMethodology";
import MagazineThinkDeeper from "@/prototypes/magazine/pages/ThinkDeeper";
import MagazineOnboarding from "@/prototypes/magazine/pages/Onboarding";
import MagazineProfile from "@/prototypes/magazine/pages/Profile";
import MagazineSettings from "@/prototypes/magazine/pages/Settings";
import MagazineBudget from "@/prototypes/magazine/pages/Budget";
import MagazineReceipt from "@/prototypes/magazine/pages/Receipt";
import MagazineLogin from "@/prototypes/magazine/pages/Login";
import MagazineRegister from "@/prototypes/magazine/pages/Register";
import MagazineForgotPassword from "@/prototypes/magazine/pages/ForgotPassword";
import MagazineResetPassword from "@/prototypes/magazine/pages/ResetPassword";
import MagazineVerifyEmail from "@/prototypes/magazine/pages/VerifyEmail";
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
import MagazineCampaignNewsResponse from "@/prototypes/magazine/pages/CampaignNewsResponse";
import MagazineCoalitionProvisions from "@/prototypes/magazine/pages/CoalitionProvisions";
import MagazineCoalitionProvisionDetail from "@/prototypes/magazine/pages/CoalitionProvisionDetail";
import MagazineCoalitionProvisionParticipate from "@/prototypes/magazine/pages/CoalitionProvisionParticipate";
import MagazineLeagues from "@/prototypes/magazine/pages/Leagues";
import MagazineLeagueDetail from "@/prototypes/magazine/pages/LeagueDetail";
import MagazineLeagueJoin from "@/prototypes/magazine/pages/LeagueJoin";
import MagazineLeagueRound from "@/prototypes/magazine/pages/LeagueRound";
import MagazineAbout from "@/prototypes/magazine/pages/About";
import MagazineZeitgeist from "@/prototypes/magazine/pages/Zeitgeist";
import MagazineCohort from "@/prototypes/magazine/pages/Cohort";

function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route path="/" element={<MagazineLayout />}>
            <Route index element={<MagazineHomeIndex />} />
            <Route path="magazine" element={<MagazineReadView />} />
            <Route
              path="briefings/who-gets-your-tax-dollar"
              element={<MagazineTaxApportionment />}
            />
            <Route
              path="briefings/who-gets-your-tax-dollar/methodology"
              element={<MagazineTaxMethodology />}
            />
            <Route path="briefings/:slug" element={<MagazineBriefingDetail />} />
            <Route path="think-deeper/:slug" element={<MagazineThinkDeeper />} />
            <Route path="onboarding" element={<MagazineOnboarding />} />
            <Route path="profile" element={<MagazineProfile />} />
            <Route path="settings" element={<MagazineSettings />} />
            <Route path="budget" element={<MagazineBudget />} />
            <Route path="receipt/:id" element={<MagazineReceipt />} />
            <Route path="login" element={<MagazineLogin />} />
            <Route path="register" element={<MagazineRegister />} />
            <Route path="forgot-password" element={<MagazineForgotPassword />} />
            <Route path="reset-password" element={<MagazineResetPassword />} />
            <Route path="verify-email" element={<MagazineVerifyEmail />} />
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
            <Route path="campaigns/:id/news/:slug" element={<MagazineCampaignNewsResponse />} />
            <Route path="coalition" element={<MagazineCoalitionProvisions />} />
            <Route path="coalition/:id" element={<MagazineCoalitionProvisionDetail />} />
            <Route path="coalition/:id/participate" element={<MagazineCoalitionProvisionParticipate />} />
            <Route path="leagues" element={<MagazineLeagues />} />
            <Route path="leagues/join/:code" element={<MagazineLeagueJoin />} />
            <Route path="leagues/:id" element={<MagazineLeagueDetail />} />
            <Route path="leagues/:id/rounds/:roundId" element={<MagazineLeagueRound />} />
            <Route path="about" element={<MagazineAbout />} />
            <Route path="zeitgeist" element={<MagazineZeitgeist />} />
            <Route path="cohort" element={<MagazineCohort />} />
          </Route>
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  );
}

export default App;
