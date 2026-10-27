// Downloads the newest app bundle (AI-Pulse-app-win-x64-<version>.zip) from
// the ai-news-desk repo's GitHub Releases and unzips it into ./payload.
//
//   node scripts/fetch-bundle.js            # latest release
//   node scripts/fetch-bundle.js v1.2.0     # a specific release
//
// The app repo is private, so set APP_TOKEN (or GITHUB_TOKEN) to a token
// that can read it. Prints the bundle version on the last line.
const fs = require("fs");
const path = require("path");
const https = require("https");
const { execSync } = require("child_process");

const OWNER = process.env.APP_OWNER || "stephanieloveshotpot";
const REPO = process.env.APP_REPO || "ai-news-desk";
const ROOT = path.join(__dirname, "..");
const PAYLOAD = path.join(ROOT, "payload");
const TOKEN = process.env.APP_TOKEN || process.env.GITHUB_TOKEN;

function get(url, headers = {}, redirects = 5) {
  return new Promise((resolve, reject) => {
    https.get(url, { headers: { "User-Agent": "ai-pulse-installer-build", ...headers } }, (res) => {
      if ([301, 302, 303, 307, 308].includes(res.statusCode) && redirects > 0) {
        res.resume();
        const sameHost = new URL(res.headers.location).host === new URL(url).host;
        return resolve(get(res.headers.location, sameHost ? headers : {}, redirects - 1));
      }
      if (res.statusCode !== 200) { res.resume(); return reject(new Error(`${url} -> HTTP ${res.statusCode}`)); }
      const chunks = [];
      res.on("data", (c) => chunks.push(c));
      res.on("end", () => resolve(Buffer.concat(chunks)));
      res.on("error", reject);
    }).on("error", reject);
  });
}

(async () => {
  const tag = process.argv[2];
  const api = `https://api.github.com/repos/${OWNER}/${REPO}/releases/${tag ? "tags/" + tag : "latest"}`;
  const auth = TOKEN ? { Authorization: `Bearer ${TOKEN}` } : {};
  const release = JSON.parse((await get(api, { ...auth, Accept: "application/vnd.github+json" })).toString());
  const asset = (release.assets || []).find((a) => /^AI-Pulse-app-win-x64-.*\.zip$/.test(a.name));
  if (!asset) throw new Error(`Release ${release.tag_name} has no AI-Pulse-app-win-x64-*.zip asset`);

  console.log(`Downloading ${asset.name} from ${release.tag_name} (${(asset.size / 1e6).toFixed(1)} MB)`);
  let buf;
  try { buf = await get(asset.browser_download_url, auth); }
  catch (e) { console.log(`Direct download failed (${e.message}); using the API asset URL`); buf = await get(asset.url, { ...auth, Accept: "application/octet-stream" }); }

  fs.rmSync(PAYLOAD, { recursive: true, force: true });
  fs.mkdirSync(PAYLOAD, { recursive: true });
  const zip = path.join(PAYLOAD, asset.name);
  fs.writeFileSync(zip, buf);
  if (process.platform === "win32") {
    execSync(`powershell -NoProfile -Command "Expand-Archive -Path '${zip}' -DestinationPath '${PAYLOAD}' -Force"`, { stdio: "inherit" });
  } else {
    execSync(`unzip -q -o "${zip}" -d "${PAYLOAD}"`, { stdio: "inherit" });
  }
  fs.rmSync(zip);

  for (const must of ["app/AI Pulse.exe", "legal/TERMS.txt", "legal/PRIVACY.txt", "VERSION"]) {
    if (!fs.existsSync(path.join(PAYLOAD, must))) throw new Error(`Bundle is missing ${must}`);
  }
  const version = fs.readFileSync(path.join(PAYLOAD, "VERSION"), "utf8").trim();
  console.log(`payload ready in ${PAYLOAD}`);
  console.log(version);
})().catch((e) => {
  const hint = /HTTP 404/.test(e.message) ? " (404: the app repo is private and this token can't read it; set APP_TOKEN)" : "";
  console.error((process.env.GITHUB_ACTIONS ? "::error::" : "") + e.message + hint);
  process.exit(1);
});
