import { marked } from "marked";
import { Link } from "react-router";
import privacyPolicy from "../../../../docs/legal/privacy-policy.md?raw";
import termsOfUse from "../../../../docs/legal/terms-of-use.md?raw";
import "./LegalPage.css";

/**
 * The privacy policy and terms of use (#121), at /privacy and /terms, open whether or not
 * someone is signed in: the stores, the apps and the web app link here. The texts are the
 * Markdown in docs/legal, so there is one copy of each; their links to each other point at
 * those files, and here at the pages.
 */
const toHtml = (markdown: string) =>
  marked.parse(markdown.replaceAll("(privacy-policy.md)", "(/privacy)").replaceAll("(terms-of-use.md)", "(/terms)"), {
    async: false,
  });

const documents = { privacy: toHtml(privacyPolicy), terms: toHtml(termsOfUse) };

export function LegalPage({ document }: { document: keyof typeof documents }) {
  return (
    <div className="legal">
      <header className="legal-header">
        <Link to="/">GolfProgress</Link>
      </header>
      {/* Our own text from the repository, not user input. In English only, for now. */}
      <main className="legal-text" lang="en" dangerouslySetInnerHTML={{ __html: documents[document] }} />
    </div>
  );
}
