import { Link } from "react-router-dom";
import LegalDoc from "./LegalDoc";
import { LEGAL_ENTITY, GOVERNING_STATE, LEGAL_CONTACT_EMAIL } from "@/lib/legal";

export default function Eula() {
  return (
    <LegalDoc
      testid="eula-page"
      kicker="License"
      title="End User License Agreement"
      lead={
        <>
          This End User License Agreement (“EULA”) governs your use of the
          Civersify and Debate Arena applications, including our mobile apps (the
          “Software”), provided by {LEGAL_ENTITY}. By installing or using the
          Software, you agree to this EULA.
        </>
      }
      sections={[
        {
          heading: "License grant",
          body: (
            <p>
              We grant you a personal, limited, non-exclusive, non-transferable,
              revocable license to install and use the Software on devices you own
              or control, solely for your personal, non-commercial use and in
              accordance with this EULA and our Terms of Service.
            </p>
          ),
        },
        {
          heading: "Restrictions",
          body: (
            <>
              <p>You may not:</p>
              <ul className="list-disc pl-5">
                <li>
                  copy, modify, or create derivative works of the Software except
                  as allowed by law;
                </li>
                <li>
                  reverse engineer, decompile, or disassemble the Software, except
                  to the extent that restriction is prohibited by applicable law;
                </li>
                <li>
                  rent, lease, lend, sell, sublicense, or otherwise transfer the
                  Software; or
                </li>
                <li>
                  remove or alter any proprietary notices, or circumvent security
                  or access controls.
                </li>
              </ul>
            </>
          ),
        },
        {
          heading: "Ownership",
          body: (
            <p>
              The Software is licensed, not sold. {LEGAL_ENTITY} and its licensors
              retain all right, title, and interest in and to the Software,
              including all intellectual property rights. This EULA grants you no
              rights other than the license expressly stated.
            </p>
          ),
        },
        {
          heading: "Updates & changes",
          body: (
            <p>
              We may provide updates, and features of the Software may be added,
              changed, or removed at any time. Any prices for paid features are
              subject to change. We may modify this EULA; continued use of the
              Software after an update constitutes acceptance.
            </p>
          ),
        },
        {
          heading: "App store terms",
          body: (
            <p>
              If you obtained the Software through a third-party app store (such as
              the Apple App Store or Google Play), your use is also subject to that
              store’s terms. Where those terms conflict with this EULA for the
              store’s benefit, the store’s applicable terms control for that
              distribution.
            </p>
          ),
        },
        {
          heading: "No warranty",
          body: (
            <p>
              THE SOFTWARE IS PROVIDED “AS IS” AND “AS AVAILABLE,” WITHOUT WARRANTY
              OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING IMPLIED WARRANTIES OF
              MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, AND
              NON-INFRINGEMENT. You use the Software at your own risk.
            </p>
          ),
        },
        {
          heading: "Limitation of liability",
          body: (
            <p>
              TO THE FULLEST EXTENT PERMITTED BY LAW, {LEGAL_ENTITY.toUpperCase()}{" "}
              WILL NOT BE LIABLE FOR ANY INDIRECT, INCIDENTAL, SPECIAL,
              CONSEQUENTIAL, OR PUNITIVE DAMAGES ARISING OUT OF OR RELATED TO YOUR
              USE OF THE SOFTWARE.
            </p>
          ),
        },
        {
          heading: "Termination",
          body: (
            <p>
              This license is effective until terminated. It terminates
              automatically if you breach it. On termination you must stop using
              and delete the Software. Sections that by their nature should survive
              will survive.
            </p>
          ),
        },
        {
          heading: "Governing law & contact",
          body: (
            <p>
              This EULA is governed by the laws of the State of {GOVERNING_STATE},
              without regard to its conflict-of-laws rules. Questions? Email{" "}
              {LEGAL_CONTACT_EMAIL}. See also our{" "}
              <Link to="/terms" className="text-[var(--accent)] underline">
                Terms of Service
              </Link>{" "}
              and{" "}
              <Link to="/privacy" className="text-[var(--accent)] underline">
                Privacy Policy
              </Link>
              .
            </p>
          ),
        },
      ]}
    />
  );
}
