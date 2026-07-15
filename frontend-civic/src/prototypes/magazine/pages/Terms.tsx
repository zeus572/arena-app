import { Link } from "react-router-dom";
import LegalDoc from "./LegalDoc";
import {
  LEGAL_ENTITY,
  LEGAL_CONTACT_EMAIL,
  GOVERNING_STATE,
  MINIMUM_AGE,
} from "@/lib/legal";

export default function Terms() {
  return (
    <LegalDoc
      testid="terms-page"
      kicker="Terms"
      title="Terms of Service"
      lead={
        <>
          These Terms are an agreement between you and {LEGAL_ENTITY} governing
          your use of Civersify and the Debate Arena (the “Service”). By creating
          an account or using the Service, you agree to these Terms.
        </>
      }
      sections={[
        {
          heading: "Eligibility",
          body: (
            <p>
              You must be at least {MINIMUM_AGE} years old to use the Service. By
              using it you represent that you meet this requirement and that the
              registration information you provide is accurate.
            </p>
          ),
        },
        {
          heading: "Your account",
          body: (
            <p>
              You are responsible for activity under your account and for keeping
              your credentials secure. Notify us promptly of any unauthorized use.
              We may offer optional security features (such as two-factor
              authentication) and recommend you enable them.
            </p>
          ),
        },
        {
          heading: "Acceptable use",
          body: (
            <>
              <p>You agree not to:</p>
              <ul className="list-disc pl-5">
                <li>break the law or infringe others’ rights;</li>
                <li>
                  harass, threaten, or impersonate others, or post unlawful,
                  hateful, or deliberately deceptive content;
                </li>
                <li>
                  disrupt, overload, scrape, or attempt to gain unauthorized
                  access to the Service; or
                </li>
                <li>
                  misuse debate, coalition, or community features to spam or
                  manipulate outcomes.
                </li>
              </ul>
              <p>
                We may remove content or suspend accounts that violate these Terms.
              </p>
            </>
          ),
        },
        {
          heading: "Your content",
          body: (
            <p>
              You keep ownership of what you post. You grant us a non-exclusive,
              worldwide, royalty-free license to host, display, and distribute your
              contributions within the Service so it can operate as intended. You
              are responsible for the content you post and represent that you have
              the right to post it.
            </p>
          ),
        },
        {
          heading: "The Service may change",
          body: (
            <p>
              We are actively building this product. Features may be added,
              changed, or removed, and the Service (or parts of it) may be
              suspended or discontinued at any time without liability to you. Any
              prices or paid features are subject to change; we will provide notice
              where required for paid plans.
            </p>
          ),
        },
        {
          heading: "AI-generated content",
          body: (
            <p>
              The Service uses AI to generate debate turns and other material. Such
              content may be inaccurate, incomplete, or not reflect any real
              person’s views, and is provided for discussion and educational
              purposes only. Do not rely on it as professional, legal, or voting
              advice.
            </p>
          ),
        },
        {
          heading: "Disclaimers",
          body: (
            <p>
              THE SERVICE IS PROVIDED “AS IS” AND “AS AVAILABLE,” WITHOUT
              WARRANTIES OF ANY KIND, WHETHER EXPRESS OR IMPLIED, INCLUDING
              MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, AND
              NON-INFRINGEMENT. We do not warrant that the Service will be
              uninterrupted, error-free, or secure.
            </p>
          ),
        },
        {
          heading: "Limitation of liability",
          body: (
            <p>
              TO THE FULLEST EXTENT PERMITTED BY LAW, {LEGAL_ENTITY.toUpperCase()}{" "}
              WILL NOT BE LIABLE FOR ANY INDIRECT, INCIDENTAL, SPECIAL,
              CONSEQUENTIAL, OR PUNITIVE DAMAGES, OR ANY LOSS OF DATA, USE, OR
              PROFITS, ARISING FROM YOUR USE OF THE SERVICE. Our total liability
              for any claim relating to the Service will not exceed the greater of
              the amount you paid us in the prior twelve months or USD $50.
            </p>
          ),
        },
        {
          heading: "Termination",
          body: (
            <p>
              You may stop using the Service at any time. We may suspend or
              terminate access if you violate these Terms or to protect the
              Service or other users. Provisions that by their nature should
              survive termination will survive.
            </p>
          ),
        },
        {
          heading: "Governing law",
          body: (
            <p>
              These Terms are governed by the laws of the State of{" "}
              {GOVERNING_STATE}, without regard to its conflict-of-laws rules. You
              agree to the exclusive jurisdiction of the state and federal courts
              located in {GOVERNING_STATE} for any dispute not subject to
              arbitration or small-claims resolution.
            </p>
          ),
        },
        {
          heading: "Changes to these Terms",
          body: (
            <>
              <p>
                Each version of these Terms is identified by the effective date
                shown at the top. We may update these Terms from time to time as the
                Service evolves. When we do, we will revise that effective date and,
                for material changes, take reasonable steps to let you know — for
                example, an in-product notice or email.
              </p>
              <p>
                <strong>
                  Your continued use of the Service after an updated version takes
                  effect constitutes your acceptance of the updated Terms.
                </strong>{" "}
                If you do not agree to a change, your remedy is to stop using the
                Service and close your account. For significant changes we may also
                ask you to re-acknowledge these Terms before you continue.
              </p>
            </>
          ),
        },
        {
          heading: "Contact",
          body: (
            <p>
              Questions? Email {LEGAL_CONTACT_EMAIL}. See also our{" "}
              <Link to="/privacy" className="text-[var(--accent)] underline">
                Privacy Policy
              </Link>{" "}
              and{" "}
              <Link to="/eula" className="text-[var(--accent)] underline">
                EULA
              </Link>
              .
            </p>
          ),
        },
      ]}
    />
  );
}
