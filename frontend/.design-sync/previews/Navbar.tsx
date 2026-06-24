import { Navbar } from "frontend";

// The full top navigation bar. Takes no props — it reads the router and auth
// context from the preview provider and renders its logged-out state (no token).
export const Default = () => <Navbar />;
