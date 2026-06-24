import { RoastStageHeader } from "frontend";

// Roast format: a spotlit comedy-club stage card pitting the two debaters,
// crown in the middle ("winner takes the crown").
export const ComedyCellar = () => (
  <RoastStageHeader
    proponent={{ name: "Senator Vale" } as any}
    opponent={{ name: "Dr. Maya Okonkwo" } as any}
  />
);
