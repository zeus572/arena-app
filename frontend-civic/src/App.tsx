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
          </Route>
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  );
}

export default App;
