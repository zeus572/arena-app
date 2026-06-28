import { MatchupIntro } from "frontend";

// The full-screen pre-debate intro, one per theme. The component's `preview`
// prop renders it inline (relative, fixed 16rem panel), but its entrance
// animations still play — so we freeze all animations to their resting frame
// (animation:none reverts each layer to its visible base state), which is what
// a static card needs: VS stamp, names, labels, and topic all shown.
const proponent = { id: "p", name: "Vale", label: "Conservative", color: "conservative" } as any;
const opponent = { id: "o", name: "Okonkwo", label: "Progressive", color: "progressive" } as any;
const topic = "Should the federal minimum wage track inflation?";

function Stage({ children }: { children: any }) {
  return (
    <>
      <style>{`*,*::before,*::after{animation:none!important}`}</style>
      {children}
    </>
  );
}

export const Arcade = () => (
  <Stage>
    <MatchupIntro preview theme="arcade" proponent={proponent} opponent={opponent} topic={topic} onComplete={() => {}} />
  </Stage>
);

export const Anime = () => (
  <Stage>
    <MatchupIntro preview theme="anime" proponent={proponent} opponent={opponent} topic={topic} onComplete={() => {}} />
  </Stage>
);

export const Boxing = () => (
  <Stage>
    <MatchupIntro preview theme="boxing" proponent={proponent} opponent={opponent} topic={topic} onComplete={() => {}} />
  </Stage>
);

export const Cinematic = () => (
  <Stage>
    <MatchupIntro preview theme="cinematic" proponent={proponent} opponent={opponent} topic={topic} onComplete={() => {}} />
  </Stage>
);
