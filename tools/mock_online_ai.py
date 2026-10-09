"""A stand-in for the online AI services, to test Menu > Advanced > AI models > Online AI without an API key or internet.
Speaks the three wire formats (OpenAI /v1/chat/completions + /v1/models, Anthropic /v1/messages + /v1/models, Gemini
:generateContent + /models) on 127.0.0.1:8890, checks the headers each real service needs, and writes every request to
tools/mock_online_ai.log (JSON lines). Answers are canned: "MOCK <service> ..." echoing the last user message.

    python tools/mock_online_ai.py [port]            then in the app: Service = Other (server http://127.0.0.1:8890), any key
    MODE=reject_temperature ...                       the OpenAI side answers 400 "Unsupported value: temperature" (a newer model)
    MODE=bad_key ...                                  every call answers 401
    MODE=slow ...                                     every call waits 40 s (the app gives up after 25 s)
"""
import json
import os
import sys
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 8890
MODE = os.environ.get("MODE", "")
LOG = Path(__file__).with_name("mock_online_ai.log")


def tone(text, rate=24000):
    """16-bit mono PCM: a soft tone, ~0.06 s per character (at least 0.5 s) - stands in for a spoken sentence."""
    import math
    import struct
    seconds = max(0.5, min(6.0, 0.06 * len(text)))
    freq = 180 + (len(text) % 7) * 20
    n = int(seconds * rate)
    return b"".join(struct.pack("<h", int(6000 * math.sin(2 * math.pi * freq * i / rate))) for i in range(n))


def wav(pcm, rate=24000):
    import struct
    return (b"RIFF" + struct.pack("<I", 36 + len(pcm)) + b"WAVEfmt " + struct.pack("<IHHIIHH", 16, 1, 1, rate, rate * 2, 2, 16) +
            b"data" + struct.pack("<I", len(pcm)) + pcm)


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def _send(self, code, obj):
        data = json.dumps(obj).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def _pcm(self, data):
        self.send_response(200)
        self.send_header("Content-Type", "audio/pcm")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def _record(self, body):
        entry = {"method": self.command, "path": self.path, "headers": {k.lower(): v for k, v in self.headers.items() if k.lower() in
                 ("authorization", "x-api-key", "anthropic-version", "x-goog-api-key", "content-type")}, "body": body}
        with LOG.open("a", encoding="utf-8") as f:
            f.write(json.dumps(entry, ensure_ascii=False) + "\n")

    def do_GET(self):
        self._record(None)
        if MODE == "bad_key":
            return self._send(401, {"error": {"message": "Incorrect API key provided"}})
        if self.path.startswith("/v1/voices"):
            if not self.headers.get("xi-api-key"):
                return self._send(401, {"detail": {"message": "missing xi-api-key"}})
            return self._send(200, {"voices": [{"name": "Mock Rachel", "voice_id": "mock-rachel"}, {"name": "Mock Adam", "voice_id": "mock-adam"}]})
        if self.path.startswith("/v1beta/models"):
            return self._send(200, {"models": [
                {"name": "models/gemini-mock-flash", "supportedGenerationMethods": ["generateContent"]},
                {"name": "models/text-embedding-mock", "supportedGenerationMethods": ["embedContent"]}]})
        if self.path.startswith("/v1/models"):
            return self._send(200, {"data": [{"id": "mock-small"}, {"id": "mock-large"}, {"id": "text-embedding-mock"}]})
        self._send(404, {"error": {"message": "not found"}})

    def do_POST(self):
        n = int(self.headers.get("Content-Length", 0))
        raw = self.rfile.read(n) or b"{}"
        if "multipart/form-data" in self.headers.get("Content-Type", ""):
            import re
            body = {"_multipart": sorted(set(re.findall(rb'name="([^"]+)"', raw))) and
                                  [m.decode() for m in sorted(set(re.findall(rb'name="([^"]+)"', raw)))], "_bytes": n}
            body["_values"] = {k.decode(): v.decode(errors="replace") for k, v in
                               re.findall(rb'name="([^"]+)"\r\n(?:[A-Za-z-]+: [^\r\n]*\r\n)*\r\n([^\r]*)\r\n', raw)}
        else:
            body = json.loads(raw)
        self._record(body)

        # ---- ears (OnlineEars): they get a recording and answer a canned sentence + a language
        if self.path.startswith("/v1/audio/transcriptions"):
            if not self.headers.get("Authorization", "").startswith("Bearer ") or "file" not in body.get("_multipart", []):
                return self._send(400, {"error": {"message": "need bearer + file"}})
            reply = {"text": "MOCK heard: where is your office?"}
            if body["_values"].get("response_format") == "verbose_json":
                reply["language"] = "tamil" if body["_values"].get("language") == "ta" else "english"
            return self._send(200, reply)
        if self.path.startswith("/speech-to-text"):
            if not self.headers.get("api-subscription-key"):
                return self._send(403, {"message": "Invalid API key"})
            code = body["_values"].get("language_code", "unknown")
            return self._send(200, {"request_id": "mock", "transcript": "MOCK heard: where is your office?",
                                    "language_code": "hi-IN" if code in ("unknown", "hi-IN") else code})
        if self.path.startswith("/v1/speech-to-text"):
            if not self.headers.get("xi-api-key") or "model_id" not in body.get("_multipart", []):
                return self._send(401, {"detail": {"message": "missing xi-api-key or model_id"}})
            return self._send(200, {"text": "MOCK heard: where is your office?", "language_code": "eng", "language_probability": 0.99})
        if ":generateContent" in self.path and "inlineData" in json.dumps(body):
            if not self.headers.get("x-goog-api-key"):
                return self._send(401, {"error": {"message": "missing key"}})
            return self._send(200, {"candidates": [{"content": {"parts": [{"text": json.dumps({"language": "fr", "text": "MOCK heard: où est votre bureau ?"})}]}}]})
        if MODE == "slow":
            time.sleep(40)
        if MODE == "bad_key":
            return self._send(401, {"error": {"message": "Incorrect API key provided"}})

        # ---- voices (OnlineVoice): each answers a short tone, so the app has real audio to play and measure
        if self.path.startswith("/v1/audio/speech"):
            if not self.headers.get("Authorization", "").startswith("Bearer ") or body.get("response_format") != "pcm":
                return self._send(400, {"error": {"message": "need bearer + response_format pcm"}})
            return self._pcm(tone(body.get("input", "")))
        if self.path.startswith("/v1/text-to-speech/"):
            if not self.headers.get("xi-api-key") or "output_format=pcm_24000" not in self.path:
                return self._send(401, {"detail": {"message": "missing xi-api-key or output_format"}})
            return self._pcm(tone(body.get("text", "")))
        if self.path.startswith("/text-to-speech"):
            if not self.headers.get("api-subscription-key"):
                return self._send(403, {"message": "Invalid API key"})
            if not body.get("language_code") or not body.get("speaker"):
                return self._send(422, {"detail": "language_code and speaker are required"})
            import base64
            return self._send(200, {"request_id": "mock", "audios": [base64.b64encode(wav(tone(body.get("text", "")))).decode()]})
        if ":generateContent" in self.path and "AUDIO" in json.dumps(body.get("generationConfig", {})):
            if not self.headers.get("x-goog-api-key"):
                return self._send(401, {"error": {"message": "missing key"}})
            import base64
            text = body["contents"][0]["parts"][0]["text"]
            return self._send(200, {"candidates": [{"content": {"parts": [{"inlineData": {
                "mimeType": "audio/L16;codec=pcm;rate=24000", "data": base64.b64encode(tone(text)).decode()}}]}}]})

        if self.path.endswith("/chat/completions"):
            if not self.headers.get("Authorization", "").startswith("Bearer "):
                return self._send(401, {"error": {"message": "missing bearer"}})
            if MODE == "reject_temperature" and "temperature" in body:
                return self._send(400, {"error": {"message": "Unsupported value: 'temperature' does not support 0.2 with this model."}})
            last = body["messages"][-1]["content"]
            return self._send(200, {"choices": [{"message": {"role": "assistant", "content": f"MOCK openai: {last[:60]}"}}]})

        if self.path.endswith("/v1/messages"):
            if not self.headers.get("x-api-key") or not self.headers.get("anthropic-version"):
                return self._send(401, {"error": {"message": "missing key or version"}})
            if "max_tokens" not in body:
                return self._send(400, {"error": {"message": "max_tokens: Field required"}})
            if body["messages"][0]["role"] != "user":
                return self._send(400, {"error": {"message": "first message must use the user role"}})
            last = body["messages"][-1]["content"]
            return self._send(200, {"content": [{"type": "text", "text": f"MOCK claude: {last[:60]}"}]})

        if ":generateContent" in self.path:
            if not self.headers.get("x-goog-api-key"):
                return self._send(401, {"error": {"message": "missing key"}})
            last = body["contents"][-1]["parts"][0]["text"]
            return self._send(200, {"candidates": [{"content": {"parts": [{"text": f"MOCK gemini: {last[:60]}"}]}}]})

        self._send(404, {"error": {"message": "not found"}})


if __name__ == "__main__":
    print(f"mock online AI on http://127.0.0.1:{PORT} (mode '{MODE or 'normal'}'), log: {LOG}")
    ThreadingHTTPServer(("127.0.0.1", PORT), Handler).serve_forever()
