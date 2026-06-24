import { CommonGroundHeader } from "frontend";

// Common Ground format: the two debaters flanking a pulsing heart as they
// converge. Avatars are coloured by each side's ideological lean.
export const SeekingCommonGround = () => (
  <CommonGroundHeader
    proponent={{ name: "Senator Vale" } as any}
    opponent={{ name: "Dr. Maya Okonkwo" } as any}
    proponentColor="conservative"
    opponentColor="progressive"
  />
);
