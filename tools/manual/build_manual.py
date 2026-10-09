"""Builds the staff manual PDF: python build_manual.py   (needs Pillow + pypdf, and Microsoft Edge for the PDF)
Pictures come from tools/manual/capture_manual_shots.cs (Unity Play mode) -> tools/manual/shots. Output: tools/manual/out/AltcoreBot-Staff-Manual.pdf
Two passes: the first prints the PDF to learn on which page each heading lands, the second fills the contents page."""
import html
import re
import sys
from pathlib import Path

from manual_lib import (E, HERE, OUT, Shots, figure, page_html, pdf_pages_text, plain_image, print_pdf)

shots = Shots()
toc = []          # (level, title, token)
body = []


def add(h):
    body.append(h)


def plain(text):
    return html.unescape(re.sub(r"<[^>]+>", "", text)).strip()


def token(level, title, shown=None):
    """Lists a heading in the contents (levels 1-3; the page number is found afterwards by searching the printed PDF)."""
    if level <= 3:
        toc.append((level, shown or plain(title), plain(title)))
        return f' id="h{len(toc) - 1}"'      # the contents entry links here
    return ""


def chapter(num, title, lead=""):
    add(f'<h1 class="chapter"{token(1, title, f"{num}  {plain(title)}")}><small>Part {num}</small>{title}</h1>')
    if lead:
        add(f'<p class="lead">{lead}</p>')


def h2(title):
    add(f'<h2{token(2, title)}>{title}</h2>')


def h3(title, crumb=None):
    c = f'<div class="crumb">{crumb}</div>' if crumb else ""
    add(f'{c}<h3{token(3, title)}>{title}</h3>')


def h4(title):
    add(f'<h4>{title}</h4>')


def p(text):
    add(f'<p>{text}</p>')


def ul(items, cls=""):
    add(f'<ul class="{cls}">' + "".join(f"<li>{i}</li>" for i in items) + "</ul>")


def steps(items):
    add('<ol class="steps">' + "".join(f"<li>{i}</li>" for i in items) + "</ol>")


def note(kind, title, text):
    names = {"tip": "Tip", "info": "Good to know", "warn": "Careful", "ok": "Good practice"}
    cls = {"tip": "", "info": " info", "warn": " warn", "ok": " ok"}[kind]
    add(f'<div class="note{cls}"><span class="t">{title or names[kind]}</span>{text}</div>')


def table(headers, rows, cls=""):
    add(f'<table class="{cls}"><thead><tr>' + "".join(f"<th>{h}</th>" for h in headers) + "</tr></thead><tbody>" +
        "".join("<tr>" + "".join(f"<td>{c}</td>" for c in r) + "</tr>" for r in rows) + "</tbody></table>")


def callout_list(items, numbers):
    """items: (label, html). Numbered like the outlines in the picture when the label is marked there."""
    numbered = sorted((i for i in items if i[0] in numbers), key=lambda i: numbers[i[0]])
    rest = [i for i in items if i[0] not in numbers]
    out = '<ol class="co">'
    for label, text in numbered:
        out += f'<li><span class="n">{numbers[label]}</span>{text}</li>'
    for label, text in rest:
        out += f'<li><span class="n dot"></span>{text}</li>'
    return out + "</ol>"


def menu_page(pid, title, crumb, lead, items, tips=(), fig_mm=None, notes=(), level=3, width=None, small=False):
    """One menu page: its picture(s) with numbered outlines, and the numbered explanation."""
    views = shots.views(pid)
    if not views:
        print("  !! no picture for", pid)
        return
    found = shots.numbers(pid)
    # Numbered 1, 2, 3... in the order the explanation lists them (only options that are marked in a picture).
    numbers = {label: i + 1 for i, label in enumerate(l for l, _ in items if l in found)}
    full = shots.info[views[0]]["w"] == 1440
    text = f'<p>{lead}</p>' + callout_list(items, numbers)
    titles = {"tip": "Tip", "info": "Good to know", "warn": "Careful", "ok": "Good practice"}
    for kind, t, n in notes:
        text += f'<div class="note{ {"tip": "", "info": " info", "warn": " warn", "ok": " ok"}[kind] }"><span class="t">{t or titles[kind]}</span>{n}</div>'
    if tips:
        text += '<div class="note"><span class="t">Tips</span><ul>' + "".join(f"<li>{t}</li>" for t in tips) + "</ul></div>"
    head = f'<div class="crumb">{crumb}</div><h{level}{token(level, title)}>{title}</h{level}>'
    if len(views) == 1:
        w = fig_mm or (58 if full else 76)
        fig = figure(shots, views[0], w, width=width or (900 if full else None), small=small and full, numbers=numbers)
        add(f'<div class="mp side" style="--figw:{w}mm">{fig}<div class="text">{head}{text}</div></div>')
    else:
        w = (fig_mm or (56 if full else 58))
        figs = "".join(figure(shots, v, w, width=width or (900 if full else None), small=True, numbers=numbers) for v in views)
        add(f'<div class="mp">{head}<div class="views">{figs}</div><div class="text">{text}</div></div>')


# --------------------------------------------------------------------- pictures used outside menu pages

PORTRAIT = (120, 330, 1320, 1745)    # head and body above the conversation panel
DOCK = (0, 1690, 1440, 2560)         # the conversation panel and the button


def portrait(sid, w_mm=55):
    sid = sid if sid.startswith("bot_") else "bot_" + sid
    return figure(shots, sid, w_mm, width=700, crop=PORTRAIT, name=sid + "_portrait", marks=False)


def dock_fig(sid, caption):
    return figure(shots, sid, 82, width=1000, crop=DOCK, name=sid + "_dock", caption=caption, small=True, marks=False)


logo = plain_image(Path(__file__).parents[2] / "Assets/UI/Companion/Brand/splash-logo.png", "logo.png", width=900)


# =============================================================================================== COVER

def cover():
    faces = "".join(
        f'<img src="img/{shots.prepare(s, width=700, crop=PORTRAIT, name=s + "_cover", frame=False)[0]}">'
        for s in ("bot_altcore_female", "bot_pni_male", "bot_chat_female"))
    add(f'''<section class="cover"><img class="logo" src="{logo}">
<h1>Staff<br><span>manual</span></h1><div class="rule"></div>
<div class="sub">AltcoreBot - the offline voice assistant. How to run it, use it and look after it: every screen, every menu page and
every setting, step by step.</div>
<div class="faces">{faces}</div>
<div class="foot">Version 5  ·  October 2026  ·  Covers the Altscape bots (Maya, Ethan), the P&amp;I bots (Pearl, Peter) and the open chat bots (Iris, Leo)</div></section>''')


def contents_placeholder():
    add("@@TOC@@")


# =============================================================================================== CONTENT

def content():
    # ------------------------------------------------------------------ 1
    chapter(1, "Welcome", "What AltcoreBot is, who the six bots are, and what to expect from them.")
    h2("Quick start")
    p("New here? This page is all you need for the first ten minutes. Everything is explained in detail in the parts that follow.")
    add('''<div class="grid g3"><div class="cell"><h4>A visitor</h4><ol class="steps"><li>Tap the <b>microphone</b>.</li><li>Ask a question in English or Hindi.</li>
<li>Tap again, or just stop talking.</li><li>Listen. Tap the button to stop the bot.</li></ol></div>
<div class="cell"><h4>You, the first day</h4><ol class="steps"><li>Start <code>Start AltcoreBot.cmd</code> and wait a minute or two.</li><li>Ask a test question out loud.</li>
<li>Tap the menu button (top right) and sign in.</li><li><b>Add your own sign-in</b>: Advanced › Staff sign-in.</li><li>Pick the bot for the venue in <b>BOTS</b>.</li>
<li>Tune <b>Camera</b> and <b>Lighting</b> on the screen you use.</li><li>Sign out.</li></ol></div>
<div class="cell"><h4>If something is wrong</h4><ol class="steps"><li>Open the menu: a <b>CHECK THIS PC</b> box lists what is missing.</li>
<li>Tap <b>?</b> on any page for a short guide to it.</li><li>See <b>Troubleshooting</b> (Part 7) for the common problems and fixes.</li></ol></div></div>''')
    h2("What is AltcoreBot?")
    p("AltcoreBot is a talking 3D assistant for a screen in your showroom, office or event stand. A visitor taps the microphone, "
      "asks a question in <b>English or Hindi</b>, and the bot answers out loud in a natural voice, with its lips moving as it speaks.")
    p("Everything runs <b>offline, on the kiosk PC itself</b>: no internet connection, no cloud service and no account to pay for, "
      "and nothing a visitor says leaves the computer. (An optional <b>Online AI</b> setting lets a cloud model write the answers; "
      "it is off unless staff switch it on - see Part 4.)")
    ul(["<b>It listens</b> with a speech recognition model (Whisper) and works out whether the visitor spoke English or Hindi.",
        "<b>It thinks</b> with a language model (Gemma) and a search of the documents you gave it.",
        "<b>It speaks</b> with a voice engine (Kokoro for English, Veena for natural Hindi) and moves its lips to match.",
        "<b>You run it</b> from a settings menu behind a staff sign-in: bots, voices, documents, camera, lighting, reports and more."])

    h2("Meet the six bots")
    p("There are three pairs, one female and one male in each, so you can pick the one that suits the venue. "
      "Only one bot is on screen at a time; staff switch between them from the menu.")
    cells = [
        ("altcore_female", "Maya", "Knowledge only", "Altscape and Altcore",
         "Answers only from the Altscape and Altcore documents. Warm, confident, with a light touch of wit."),
        ("altcore_male", "Ethan", "Knowledge only", "Altscape and Altcore", "Same documents and same job as Maya, with a male look and voice."),
        ("pni_female", "Pearl", "Knowledge only", "P&amp;I - Pavilions &amp; Interiors",
         "Front-desk assistant for P&amp;I, the exhibition, museum and interiors company. Understands \"PNI\" too."),
        ("pni_male", "Peter", "Knowledge only", "P&amp;I - Pavilions &amp; Interiors", "Same documents and same job as Pearl, with a male look and voice."),
        ("chat_female", "Iris", "Open chat", "Anything", "A curious, upbeat companion who loves art, music, film, travel and food. Chats about almost anything."),
        ("chat_male", "Leo", "Open chat", "Anything", "A quick-witted, kind companion who likes science, sport, cooking and a good story."),
    ]
    order = [0, 2, 4, 1, 3, 5]          # a row of women, a row of men: each column is one family of bots
    cells = [cells[i] for i in order]
    for row in (cells[:3], cells[3:]):
        add('<div class="bots">' + "".join(
            f'<div class="bot">{portrait(s)}<h4>{n}</h4><span class="tag">{kind}</span><p><b>About:</b> {about}</p><p>{desc}</p></div>'
            for s, n, kind, about, desc in row) + "</div>")
    add('<div class="nobreak">')
    table(["", "Maya", "Ethan", "Pearl", "Peter", "Iris", "Leo"], [
        ["Kind", "Knowledge only", "Knowledge only", "Knowledge only", "Knowledge only", "Open chat", "Open chat"],
        ["English voice", "Heart", "Michael", "Heart", "Michael", "Bella", "Fenrir"],
        ["Hindi voice", "Kavya", "Vinaya", "Kavya", "Vinaya", "Alpha", "Vinaya"],
        ["Documents", "5", "5 (same)", "1", "1 (same)", "none", "none"]], "plain")
    add('</div>')

    h4("What the Knowledge only bots know")
    table(["Bots", "Documents they answer from"], [
        ["Maya and Ethan", "Altcore Corporate Profile Deck · Altscape Mobile Deck · Altscape ecosystem (PDF and Word) · Altscape Sales Operating System Handbook"],
        ["Pearl and Peter", "Pavilions and Interiors Knowledge Base (Word)"]], "plain")
    p("Each pair shares its documents, so adding or removing a file for one adds or removes it for the other, and a taught answer is known by both.")

    h2("Two kinds of bot")
    table(["", "Knowledge only", "Open chat"], [
        ["Who", "Maya, Ethan, Pearl, Peter", "Iris, Leo"],
        ["Answers from", "Only the documents you give it. Anything else is politely refused.",
         "Its own general knowledge and personality - about anything."],
        ["Good for", "Company information, products, services: anything where the facts must be right.",
         "Greeting and entertaining visitors, small talk, a friendly face at a stand."],
        ["Risk", "None of making facts up: if it isn't in the documents, it says so.",
         "It can be wrong about facts, and it doesn't know today's news."],
        ["Memory", "Follow-up questions (\"does <i>it</i> work on mobile?\") work for a couple of minutes.",
         "Remembers the last 6 exchanges, then starts fresh for a new visitor."]])
    note("tip", None, "Company bots should stay on <b>Knowledge only</b>. Switching one to Open chat lets it make up answers about your company.")

    h2("What it can and can't do")
    table(["It can", "It can't"], [
        ["Answer questions from the documents you add (PDF, Word, PowerPoint, text).", "Look anything up on the internet by itself. It answers from its documents (or, Open chat, from what its language model knows)."],
        ["Understand English and Hindi, including Indian-accented English, and answer in the same language.", "Speak or understand other languages yet."],
        ["Speak with a natural voice and move its lips to match.", "Hear well in a very loud room - see the noise settings and the microphone advice later."],
        ["Be taught better answers by staff, which it uses from the very next question.", "Learn by itself. Nothing is learnt without staff choosing it."],
        ["Keep a log of questions and report how busy the kiosk was.", "Know what happened today: news, weather, sports scores."]], "plain")

    h2("How an answer is made")
    p("Knowing the steps helps you understand why a bot sometimes says \"I can't help with that one\". "
      "A <b>Knowledge only</b> bot checks twice that an answer really comes from your documents:")
    add('''<div class="flow"><div class="box"><b>1 · Ears</b>Whisper turns the visitor's voice into text and detects English or Hindi.</div><div class="arr">›</div>
<div class="box"><b>2 · Search</b>The question is matched against the documents. Too far from them? A polite refusal.</div><div class="arr">›</div>
<div class="box"><b>3 · Brain</b>Gemma writes the answer using only the best-matching passages.</div><div class="arr">›</div>
<div class="box"><b>4 · Check</b>The answer is compared with the documents again. Not backed up? Not spoken.</div><div class="arr">›</div>
<div class="box"><b>5 · Voice</b>The answer is spoken, sentence by sentence, with lip-sync.</div></div>''')
    p("An <b>Open chat</b> bot skips the document steps: it answers straight from its personality and general knowledge. "
      "For Hindi questions the answer is written in English first, then translated into everyday spoken Hindi one sentence at a time, so the voice can start early.")
    note("info", None, "The first words usually start 1 to 4 seconds after the visitor stops talking. Hindi takes a little longer than English because of the translation step.")

    # ------------------------------------------------------------------ 2
    chapter(2, "Setting up the kiosk", "What you need, how to install it, and how to start and stop it.")
    h2("What you need")
    table(["", "Requirement"], [
        ["Computer", "Windows 10 or 11 PC with an <b>NVIDIA graphics card</b> (8 GB of graphics memory or more; 12 GB+ recommended, 16 GB is ideal) and a current NVIDIA driver."],
        ["Memory and disk", "16 GB of RAM or more, and about 21 GB of free disk space for the app folder."],
        ["Screen", "A touch screen or TV. The app is made for a <b>portrait 2K or 4K</b> screen, and also adapts to a wide screen."],
        ["Sound", "A microphone (a close, directional or headset microphone works best) and speakers or the TV's speakers."],
        ["Internet", "<b>Not needed.</b> Nothing to download or sign in to - unless you switch on the optional Online AI."]], "plain")
    note("warn", None, "Without an NVIDIA card the bot will be very slow or won't start. The loading screen and the top of the menu tell you if something is missing (see <b>CHECK THIS PC</b> in the Troubleshooting part).")

    h2("What is in the folder")
    table(["Item", "What it is"], [
        ["<code>AltcoreBot.exe</code>", "The app itself."],
        ["<code>Start AltcoreBot.cmd</code>", "The best way to start it: the app is started again by itself if it ever crashes or freezes."],
        ["<code>TTSServer</code>", "The voice and document-search engine (starts and stops with the app)."],
        ["<code>TTSRuntime</code>", "A private copy of Python and the search models. You never need to install Python."],
        ["<code>AltcoreBot_Data</code>", "The app's data, including the bots' <b>documents</b> (<code>StreamingAssets\\Knowledge</code>) and made bots (<code>StreamingAssets\\Bots</code>)."],
        ["<code>README.txt</code>", "A short install note."],
        ["<code>watchdog.log</code>", "Appears after the first run: a note of every restart."]], "plain")

    h2("Install and first start")
    steps(["Copy the <b>whole folder</b> to the PC, for example <code>C:\\Altcore</code>. Do <b>not</b> put it in <i>Program Files</i>: the app writes into its own folder (taught answers, the voice engine).",
           "Double-click <code>Start AltcoreBot.cmd</code>. A black loading screen appears (below).",
           "Wait. The first start takes <b>a minute or two</b> while the AI loads. Later starts are quicker.",
           "When the loading screen fades, the first bot (Maya) appears and the welcome line shows. The kiosk is ready.",
           "On a portrait TV, set the display to <b>Portrait</b> in Windows display settings. The app adapts by itself."])
    add('<div class="mp side" style="--figw:58mm">' + figure(shots, "loading", 58, width=900, small=True) +
        '''<div class="text"><h3>The loading screen</h3><p>It shows the app starting. The ring fills as parts load, and three chips turn from a dot to a spinner to a tick:</p>
<ol class="co"><li><span class="n">1</span><b>Percentage and bar</b> - overall progress. The first start is the longest.</li>
<li><span class="n">2</span><b>Brain, Listening, Voice</b> - the three parts that must be ready. The language model loads first, then speech recognition, while the voice and document search load beside them.</li></ol>
<p>If something goes wrong the screen says <b>Couldn't start</b> and gives the reason. See Troubleshooting.</p></div></div>''')

    h2("Make it start by itself")
    steps(["Press <span class='k'>Win</span> + <span class='k'>R</span>, type <code>shell:startup</code> and press Enter. The Startup folder opens.",
           "Right-click <code>Start AltcoreBot.cmd</code>, choose <b>Create shortcut</b>, and drag the shortcut into the Startup folder.",
           "Restart the PC once to check. The kiosk now starts when Windows does."])
    note("tip", None, "Set Windows to sign in automatically and to never sleep or turn off the screen, so a power cut or restart brings the kiosk back with nobody there.")

    h2("Closing and restarting")
    ul(["<b>Close the app properly:</b> open the menu, scroll to the bottom and tap <b>Close the app</b>. It asks for a second tap, so it can't happen by accident. Started with the <code>.cmd</code>, this also stops the auto-restart.",
        "<b>If it crashes or freezes:</b> started with <code>Start AltcoreBot.cmd</code>, a crash is followed by a restart within seconds, and a freeze after about two minutes. <code>watchdog.log</code> says when and why. If it stops 5 times in 10 minutes it gives up, so the problem can be seen and fixed.",
        "<b>If only the voice engine stops:</b> the app starts it again by itself (the bot is quiet for a minute). If it stops 3 times in 10 minutes you will see <i>The voice engine keeps stopping - restart the app</i>.",
        "<b>Switching the PC off:</b> close the app first, then shut down Windows as usual."])

    h2("Privacy and where things are kept")
    p("Everything stays on this PC (unless staff switch on Online AI: then the question and the matching document passages go to the chosen service). Visitors' questions are kept in a small log so you can see what people ask. You can switch that off "
      "(Menu › Questions &amp; answers › Save conversations). Settings are kept per PC, so a copied folder starts with the settings it was delivered with.")
    table(["What", "Where"], [
        ["Documents and taught answers", "<code>AltcoreBot_Data\\StreamingAssets\\Knowledge\\&lt;bot&gt;</code> (the menu's <b>Open the documents folder</b> opens it)"],
        ["Bots made in the app", "<code>AltcoreBot_Data\\StreamingAssets\\Bots</code>"],
        ["Backdrop pictures", "<code>AltcoreBot_Data\\StreamingAssets\\Stage Pictures</code>"],
        ["Visitor log (questions and answers)", "<code>C:\\Users\\&lt;user&gt;\\AppData\\LocalLow\\Altcore\\AltcoreBot\\Conversations</code>"],
        ["Settings you change in the menu", "The Windows registry of the signed-in user (<code>HKEY_CURRENT_USER\\Software\\Altcore\\AltcoreBot</code>)"],
        ["Restart notes", "<code>watchdog.log</code> next to <code>AltcoreBot.exe</code>"]], "plain")

    # ------------------------------------------------------------------ 3
    chapter(3, "Using the kiosk", "What visitors see and do. Nothing to install or sign in for visitors.")
    h2("The main screen")
    add('<div class="mp side" style="--figw:62mm">' + figure(shots, "main_ready", 62, width=900, small=True) + '''<div class="text">
<p>The 3D bot fills the screen. At the bottom is the conversation panel with the button that does almost everything.</p>
<ol class="co">
<li><span class="n">1</span><b>Menu button</b> (top right) - for staff only. It asks for a sign-in.</li>
<li><span class="n">2</span><b>Conversation panel</b> - the visitor's question and the bot's answer appear here.</li>
<li><span class="n">3</span><b>Welcome line</b> - says what the bot can talk about: <i>Ask me anything about Altscape and Altcore.</i> Each bot has its own.</li>
<li><span class="n">4</span><b>Microphone button</b> - tap to talk. It turns red while listening, shows a spinner while thinking and a speaker while the bot talks.</li>
<li><span class="n">5</span><b>State line</b> - <i>Tap to talk</i>, <i>Listening…</i>, <i>Thinking…</i> or <i>Speaking · tap to stop</i>.</li></ol></div></div>''')

    h2("Asking a question")
    p("The button goes through four states. The picture below shows each one on the real screen.")
    add('<div class="states">' +
        dock_fig("main_ready", "<b>Ready.</b> Tap the microphone and ask.") +
        dock_fig("main_listening", "<b>Listening.</b> The button is red. Tap again when done, or just stop talking in Automatic mode.") +
        dock_fig("maya_en_thinking", "<b>Thinking.</b> The question shows as text while the bot works out the answer (usually 1 to 4 seconds).") +
        dock_fig("maya_en_speaking", "<b>Speaking.</b> The answer appears as it is spoken. Tap the button to stop it.") + "</div>")
    steps(["Tap the <b>microphone</b>. The button turns red and says <i>Listening…</i>",
           "Ask your question in English or Hindi, in a normal voice, close to the microphone.",
           "Tap the button again (or in Automatic mode just stop talking). The question appears as text.",
           "The bot thinks for a moment and then answers out loud. Ask a follow-up straight away, or tap the button to stop it."])
    note("tip", "Good questions", "Short, clear and about the bot's topic. Follow-ups work too: <span class='q'>\"Does it work on mobile?\"</span>")
    table(["Bot", "Questions to try"], [
        ["Maya, Ethan", "<span class='q'>\"What is Altscape?\"</span> · <span class='q'>\"How can I contact Altcore?\"</span> · <span class='q'>\"What does the Altscape ecosystem include?\"</span>"],
        ["Pearl, Peter", "<span class='q'>\"What does P&amp;I do?\"</span> · <span class='q'>\"What does PNI do?\"</span> · <span class='q'>\"What kind of projects have you delivered?\"</span>"],
        ["Iris, Leo", "<span class='q'>\"Can you recommend a good film for the weekend?\"</span> · <span class='q'>\"Tell me something interesting about space.\"</span> · <span class='q'>\"What shall I cook tonight?\"</span>"],
        ["Any bot", "<span class='q'>\"Hello!\"</span> · <span class='q'>\"Who are you?\"</span> · <span class='q'>\"Thank you.\"</span> · a Hindi question such as <span class='q'>\"आप कौन हैं?\"</span>"]], "plain")

    h2("Two ways to start talking")
    table(["", "Tap the button (default)", "Automatic"], [
        ["How", "Tap the microphone, talk, tap again.", "Just start talking. A short pause ends the question."],
        ["Best for", "Busy or noisy places: only deliberate taps start it.", "Quiet places, hands-free."],
        ["Notes", "Holding <span class='k'>Space</span> on an attached keyboard also talks.", "It does not listen while the bot speaks or while the menu is open. The button still works."]], "plain")
    p("Staff choose the mode in Menu › Listening &amp; language. In Automatic mode the state line reads <i>Just start talking</i>.")

    h2("When nothing is heard")
    add('<div class="mp side" style="--figw:62mm">' + figure(shots, "main_nocatch", 62, width=900, small=True) + '''<div class="text">
<p>If nothing clear was heard, the panel says <b>Didn't catch that - try again.</b> This is normal in a noisy room or when a visitor spoke too quietly.</p>
<ul><li>Move closer to the microphone and speak a little louder.</li><li>Tap the button and try again.</li>
<li>If it happens a lot, see <b>Microphone and noise</b> in the Troubleshooting part.</li></ul>
<p>In <b>Automatic</b> mode a recording with no clear voice is simply ignored, with no message, so a busy room isn't taken for questions.</p></div></div>''')

    h2("Typing instead of talking")
    add('<div class="mp side" style="--figw:62mm">' + figure(shots, "main_typing", 62, width=900, small=True) + '''<div class="text">
<p>When staff switch on <b>Show keyboard</b> (Menu › Display), a keyboard button appears beside the microphone.</p>
<ol class="co"><li><span class="n">1</span><b>Keyboard button</b> - opens the text box and an on-screen keyboard.</li>
<li><span class="n">2</span><b>Text box</b> - type the question.</li><li><span class="n">3</span><b>Send</b> - the orange arrow asks the bot, exactly as if it had been spoken.</li></ol>
<p>Tap the keyboard button again to close it. Typing is useful in very noisy places, or for visitors who prefer it.</p></div></div>''')

    h2("English and Hindi")
    add('<div class="mp side" style="--figw:62mm">' + figure(shots, "maya_hi_speaking", 62, width=900, small=True) + '''<div class="text">
<ul><li>A <b>Hindi question gets a Hindi answer</b>; an English question gets an English answer. The same bot, the same voice.</li>
<li>Brand and product words stay in English letters inside Hindi answers (<i>platform</i>, <i>real estate</i>), as people really speak.</li>
<li>Staff can force one language: <b>Listens for</b> (what visitors speak) and <b>Replies in</b> (what the bot answers) in Menu › Listening &amp; language.</li>
<li>Hindi voices: <b>Veena</b> (natural, recommended) or the plainer Kokoro Hindi voices.</li></ul>
<p>The visitor's words appear exactly as heard. If a name was misheard, the answer may miss the point: ask again, more slowly.</p></div></div>''')

    h2("What each bot will and won't answer")
    p("<b>A Knowledge only bot</b> answers only from its documents. For anything else it gives a friendly refusal that points back to its topics:")
    add('<div class="mp side" style="--figw:62mm">' + figure(shots, "maya_refuse_speaking", 62, width=900, small=True) + '''<div class="text">
<ul><li>A question about the cricket match isn't in the Altscape documents, so Maya declines and steers back to what she knows.</li>
<li>Greetings and thanks (<i>Hello!</i>, <i>Who are you?</i>, <i>Thank you</i>) are answered from written lines, never invented.</li>
<li>If the documents answer only part of a question, the bot says what it doesn't know: <i>"I don't have details on pricing, but you can book a demo."</i></li></ul>
<p>Staff see every question the bot couldn't answer (Menu › Questions &amp; answers) and can teach it the answer in seconds.</p></div></div>''')

    add('<div class="grid g2" style="margin-top:5mm">'
        '<div class="cell">' + figure(shots, "pearl_pni_speaking", 76, width=900, small=True, caption="Pearl and Peter answer for P&amp;I. Visitors may say <b>PNI</b>: it is understood as P&amp;I.") + '</div>'
        '<div class="cell">' + figure(shots, "iris_chat_speaking", 76, width=900, small=True, caption="Iris and Leo are open chat bots: they talk about anything, in their own voice.") + '</div></div>')
    note("info", "Why PNI works", "\"P&amp;I\" and \"PNI\" sound almost the same, and speech recognition often writes P&amp;I as something else. The P&amp;I bots are told that PNI (and similar mishearings) mean P&amp;I, and are given the company's terms to listen for, so questions like <i>\"What does PNI do?\"</i> work.")

    h2("When the screen goes quiet")
    ul(["About <b>6 seconds</b> after the bot finishes speaking, the question and answer clear and the welcome line comes back, ready for the next visitor.",
        "After about <b>2 minutes</b> without a question, the bot treats the next person as a new visitor: follow-up questions and open chat memory start fresh.",
        "Staff can change both times (Menu › Display &amp; performance and Brain)."])

    # ------------------------------------------------------------------ 4
    chapter(4, "The staff menu", "A page-by-page guide to everything staff can change. Changes are saved at once and stay on this PC.")
    h2("Open the menu and sign in")
    add('<div class="mp side" style="--figw:62mm">' + figure(shots, "login", 62, width=900, small=True) + '''<div class="text">
<ol class="co"><li><span class="n">1</span><b>Username</b> - your own name. It is not case-sensitive.</li>
<li><span class="n">2</span><b>Password</b> - typed with the on-screen keyboard on a touch screen.</li>
<li><span class="n">3</span><b>Sign in</b> - opens the menu.</li></ol>
<ul><li>Tap the round menu button at the top right of the screen. The sign-in card appears. <b>Cancel</b> closes it.</li>
<li>Your username and password are set by whoever installed the kiosk. Each person can have their own (<i>Staff sign-in</i>).</li>
<li>After <b>5 wrong tries in a row</b> sign-in pauses for 30 seconds.</li>
<li>Once signed in, closing and reopening the menu within <b>2 minutes</b> doesn't ask again. <b>Sign out</b> at the bottom ends it at once.</li></ul></div></div>''')
    note("ok", None, "Always sign out when you finish, so visitors can't open the menu. The menu also closes by itself when you tap outside it or press <span class='k'>Esc</span>.")

    h2("The menu at a glance")
    p("The first page lists everything. Settings are grouped by <b>what they belong to</b>: the bot on screen, the visitors, this computer's devices, the display, and the advanced AI settings.")
    menu_page("root", "The settings menu (first page)", "Menu",
              "The first page of the menu, in two screens. Tap any row to open its page; the <b>?</b> at the top of any page opens that page's short guide.",
              [("Help &amp; guides".replace("&amp;", "&"), "<b>Help &amp; guides</b> - short guides to every page, for first-time users."),
               ("BOTS", "<b>BOTS</b> - tap a bot to bring it on screen. The tick marks who is talking now. Each row says what the bot knows."),
               ("New bot…", "<b>New bot…</b> - make another bot from one of the 3D characters."),
               ("Import a bot…", "<b>Import a bot…</b> - load a bot exported from another kiosk (a <code>.altbot</code> file)."),
               ("MAYA", "<b>The bot's own section</b> (named after the bot on screen) - its look and voice, personality, conversation, documents, camera, backdrop and lighting."),
               ("Look & voice", "<b>Look &amp; voice</b> and the rows under it - one page each. The grey text shows the current choice."),
               ("Camera", "<b>Camera</b> - how the bot is framed on screen (this row shows <i>Adjusted</i> once you've changed it)."),
               ("Copy, export, reset or delete", "<b>Copy, export, reset or delete</b> - look after this bot."),
               ("VISITORS", "<b>VISITORS</b> - listening and language, the questions people asked, and the report. The same for every bot."),
               ("DEVICES", "<b>DEVICES</b> - the microphone, speaker and (with several screens) the screen this PC uses."),
               ("DISPLAY", "<b>DISPLAY</b> - light or dark, the keyboard button and the debug panel."),
               ("ADVANCED", "<b>ADVANCED</b> - AI models, staff sign-in and fine-tuning pages (voice, listening, mouth, eyes, body, brain and display)."),
               ("starts:Sign out", "<b>Sign out</b> - leave staff mode."),
               ("Close the app", "<b>Close the app</b> - tap twice to confirm.")],
              tips=["A small note beside a row, like <i>3 to review</i> or <i>Adjusted</i>, tells you something there needs attention or was changed.",
                    "Scrolling: swipe up and down. The menu remembers where you were when you go back."],
              fig_mm=58)

    h3("What is for one bot, and what is for all")
    table(["For the bot on screen", "For every bot"], [
        ["Look &amp; voice, Personality, Conversation, Documents*, Camera, Backdrop, Lighting, Copy / export / reset / delete",
         "Listening &amp; language, Questions &amp; answers, Report, Microphone, Speaker, Screen, Display, AI models, Staff sign-in and all Advanced pages"]], "plain")
    p("* Bots that share documents (Maya and Ethan; Pearl and Peter) get the same document changes and learn the same taught answers, so they answer alike.")

    menu_page("help", "Help & guides", "Menu › Help & guides",
              "A short guide to every menu page, written for someone using it for the first time. The same guide opens from the <b>?</b> button at the top of each page.",
              [("Using the kiosk", "<b>A guide</b> - tap one to read it. They are grouped: Getting started, The bot on screen, Visitors, This PC and Advanced.")],
              level=3, fig_mm=60)
    menu_page("guide", "Reading a guide", "Menu › Help & guides › a guide",
              "Each guide is one short screen: what the page is for, a picture of the page with the options you need outlined and numbered, a short numbered list, and tips.",
              [], tips=["<b>Go to this page</b> at the bottom of a guide jumps to the real page.", "<b>All guides</b> at the top returns to the list."],
              level=4, fig_mm=60)

    # ---- BOTS
    h2("Bots")
    h3("Switching, adding and importing bots", "Menu › BOTS")
    ul(["<b>Switch:</b> tap a bot's name in the BOTS list. The new bot takes about a second to appear, and the welcome line changes to match. The app starts with the first bot (Maya).",
        "<b>New bot…</b> opens the New bot page (next).",
        "<b>Import a bot…</b> opens a file dialog; pick a <code>.altbot</code> file (for example from a USB drive). The bot appears in the list and becomes the active one."])
    menu_page("newbot", "New bot", "Menu › BOTS › New bot…",
              "Make another bot in three choices. It starts with the voices and personality of the bot that has the same look, so you only change what's different.",
              [("NAME", "<b>Name</b> - type it, or tap <b>Speak</b> and say it, or <b>Keyboard</b> for the on-screen keyboard."),
               ("LOOK", "<b>Look</b> - pick one of the six 3D characters. Several bots can share one."),
               ("Kind", "<b>Kind</b> - <b>Knowledge only</b> (answers from documents you give it) or <b>Open chat</b> (talks about anything)."),
               ("Create bot", "<b>Create bot</b> - makes it and switches to it. A Knowledge only bot opens the Documents page so you can add its documents straight away.")],
              tips=["A Knowledge only bot can't answer anything until it has documents.", "Made bots can be copied, exported and deleted. The six built-in bots can't be deleted."],
              level=4)

    # ---- bot pages
    h2("The bot's own pages")
    p("These rows sit under the bot's name on the first page and change only the bot on screen. To edit a different bot, switch to it first.")
    menu_page("lookvoice", "Look & voice", "Menu › Maya › Look & voice",
              "How the bot looks and sounds.",
              [("LOOK", "<b>LOOK</b> - tap a character to change the bot's body and face. A line under each name shows which other bots use it."),
               ("VOICE", "<b>VOICE</b> - the voices and the speed."),
               ("English voice", "<b>English voice</b> - opens the list of voices. Tap a voice to hear it and choose it."),
               ("Hindi voice", "<b>Hindi voice</b> - the voice for Hindi answers. Veena voices are the most natural."),
               ("Speaking speed", "<b>Speaking speed</b> - 0.7× to 1.3×. Veena voices always speak at their own natural pace.")],
              tips=["Keep both voices the same gender: Hindi answers use matching verb forms.", "A voice marked <i>Veena</i> is the same person in English and Hindi."])
    h4("The voice lists")
    add('<div class="mp"><div class="views">' + figure(shots, "voices_en", 56, width=900, small=True, caption="English voices") +
        figure(shots, "voices_hi", 56, width=900, small=True, caption="Hindi voices") +
        '<div class="text" style="flex:1"><p>Voices are grouped <b>FEMALE</b> and <b>MALE</b>. Tap one to hear a sample and select it (the tick moves).</p>'
        '<table class="plain"><tr><td><b>Kokoro</b></td><td>English: 28 voices, American and British. <b>Heart</b>, <b>Bella</b> and <b>Michael</b> are the clearest. Four plainer Hindi voices.</td></tr>'
        '<tr><td><b>Veena</b></td><td>Natural Indian-accented voices: Kavya and Maitri (female), Vinaya (male). Spoken in English and Hindi by the same person.</td></tr></table>'
        '<p>The grade beside each voice (A to F) is how well it was rated in testing.</p></div></div></div>')

    menu_page("personality", "Personality", "Menu › Maya › Personality",
              "Who the bot is and how it talks. This does not change what it knows: that comes from its documents.",
              [("NAME", "<b>NAME</b> - what the bot calls itself. Type, or tap <b>Speak</b> to say it."),
               ("TOPICS", "<b>TOPICS</b> - what the bot is about, separated by commas (Knowledge only bots). Used in <i>Ask me anything about…</i> and in refusals. Empty = taken from the documents."),
               ("WHO IT IS", "<b>WHO IT IS</b> - a few plain lines describing its character and role."),
               ("STYLE", "<b>STYLE</b> - tone, formality and humour. These apply immediately."),
               ("Tone", "<b>Tone</b> - Warm, Professional, Playful, Calm or Enthusiastic (next page)."),
               ("HOW IT TALKS", "<b>HOW IT TALKS</b> - e.g. <i>simple Indian English, says ji to be polite</i>."),
               ("ALWAYS", "<b>ALWAYS</b> - short rules it should follow, e.g. <i>end by offering more help</i>."),
               ("NEVER", "<b>NEVER</b> - rules it must not break, e.g. <i>promise discounts</i>."),
               ("Save", "<b>Save</b> - the text boxes are kept only after you tap Save.")],
              tips=["Open chat bots also have <b>TOPICS TO AVOID</b>, e.g. <i>politics, religion</i>.",
                    "Text boxes can be filled by voice: tap <b>Speak</b> beside the box and talk."])
    menu_page("tone", "Tone", "Menu › Maya › Personality › Tone",
              "Pick how the bot sounds in words.",
              [("Warm", "<b>Warm</b> (friendly and kind), <b>Professional</b> (clear, courteous, businesslike), <b>Playful</b> (lively, a bit cheeky), <b>Calm</b> (gentle and reassuring), <b>Enthusiastic</b> (upbeat and energetic). Tapping one selects it and goes back.")],
              level=4, fig_mm=60)

    menu_page("conversation", "Conversation (Knowledge only bot)", "Menu › Maya › Conversation",
              "How the bot answers: only from its documents, or about anything.",
              [("MODE", "<b>MODE</b> - how it answers, and how long."),
               ("Answers", "<b>Answers</b> - <b>Knowledge only</b> refuses anything outside its documents; <b>Open chat</b> talks about anything."),
               ("Reply length", "<b>Reply length</b> - from one line to detailed."),
               ("KNOWLEDGE", "<b>KNOWLEDGE</b> - the settings for document answers."),
               ("Topic strictness", "<b>Topic strictness</b> - how closely a question must match the documents. Higher refuses more questions that only loosely match."),
               ("Answer checking", "<b>Answer checking</b> - how closely an answer must match the documents before it is spoken. Higher blocks more made-up answers (and some good ones)."),
               ("Passages read per question", "<b>Passages read per question</b> - more finds answers spread over several places, but is a little slower.")],
              tips=["Company bots should stay on Knowledge only.", "Leave strictness and checking alone unless good questions are refused or wrong answers get through."])
    menu_page("conversation_chat", "Conversation (Open chat bot)", "Menu › Iris › Conversation",
              "The same page for an Open chat bot: the document settings are replaced by personality settings.",
              [("Answers", "<b>Answers</b> is on <b>Open chat</b>."),
               ("Reply length", "<b>Reply length</b> - 1 line, Short, Medium or Detailed."),
               ("OPEN CHAT", "<b>OPEN CHAT</b> - settings for free conversation."),
               ("Memory", "<b>Memory</b> - how many earlier exchanges it remembers (0 to 12)."),
               ("Creativity", "<b>Creativity</b> - plain, balanced or playful: how varied its replies are."),
               ("Asks questions back", "<b>Asks questions back</b> - lets it ask the visitor something to keep chatting."),
               ("Topics to avoid", "<b>Topics to avoid</b> - subjects it steers away from politely (opens Personality).")],
              level=4)

    menu_page("documents", "Documents", "Menu › Maya › Documents",
              "What a Knowledge only bot knows. It answers only from these files. (Open chat bots have no Documents row.)",
              [("contains:DOCUMENTS", "<b>MAYA'S DOCUMENTS</b> - the bot's files with size and date. Tap one to see it or remove it."),
               ("Add documents…", "<b>Add documents…</b> - opens the Windows file dialog. Pick one or several PDF, Word, PowerPoint or text files, from the PC or a USB drive."),
               ("starts:Same for", "<b>Same for Ethan</b> - on, both bots get the same change so they answer alike. Switch it off to change only this bot."),
               ("Open the documents folder", "<b>Open the documents folder</b> - shows the files in Windows Explorer.")],
              tips=["New files are read in the background. The bot can answer from them within a minute (longer for big files).",
                    "Accepted: <code>.pdf</code>, <code>.docx</code>, <code>.pptx</code>, <code>.txt</code>, <code>.md</code>.",
                    "For Hindi, Word or text files work best. Hindi PDFs often read back scrambled.",
                    "A scanned PDF that is only pictures of pages has no text to read: use the original Word file."])
    menu_page("document", "A single document", "Menu › Maya › Documents › a file",
              "Tap a file to see when it was added, and to remove it.",
              [("starts:Remove from", "<b>Remove from Maya and Ethan</b> - tap twice to confirm. The file goes to the Recycle Bin, so it can be restored from there.")],
              notes=[("info", None, "The picture shows an example file. Your own documents appear with their real names.")], level=4, fig_mm=60)

    h4("Camera, backdrop and lighting")
    p("These three pages sit <b>low on the screen</b> and the background stays clear, so you see the bot change while you drag. "
      "Each is saved for the bot on screen (the camera also separately for tall and wide screens).")
    menu_page("camera", "Camera", "Menu › Maya › Camera",
              "How the bot is framed. Tap a ready-made shot, then fine-tune with the sliders.",
              [("starts:SHOTS", "<b>SHOTS</b> - Relaxed, Straight on, Chest up, Close-up (and angled, low, cinematic), Face, Three-quarter, Low angle, Across the desk, Cinematic and Full body. Tap one to use it; the tick shows the current one."),
               ("POSITION", "<b>POSITION</b> - <b>Distance</b>, <b>Height</b>, <b>Sideways</b>, <b>Angle</b> and <b>Tilt</b> sliders. Drag; the bot moves at once and it is saved when you let go."),
               ("Distance", "<b>Distance</b> - how far the camera is from the bot (1 to 6 m)."),
               ("LENS & MOVEMENT", "<b>LENS &amp; MOVEMENT</b> - the lens and the follow switch (below the last picture)."),
               ("Lens", "<b>Lens</b> - smaller is flatter and more flattering (move back to fit); larger is wide-angle."),
               ("Follow the character", "<b>Follow the character</b> - the camera gently re-centres as the bot moves."),
               ("Reset camera", "<b>Reset camera</b> - back to the character's own framing.")],
              tips=["On a tall (portrait) TV, <i>Relaxed</i> or <i>Chest up</i> show the face and the hands that gesture while talking.",
                    "The camera is kept for each character, separately for tall and wide screens."], fig_mm=40)
    menu_page("stage", "Backdrop", "Menu › Maya › Backdrop",
              "What is behind the bot.",
              [("BACKDROP", "<b>BACKDROP</b> - four styles: <b>Studio</b> (a soft photo-studio look with a shadow on the floor), <b>Gradient</b>, <b>Picture</b> (a softly blurred photo) and <b>Flat colour</b>."),
               ("Studio", "<b>Studio</b> - the tick shows the style in use."),
               ("ADJUST", "<b>ADJUST</b> - how bright the backdrop is."),
               ("Brightness", "<b>Brightness</b> - lighter or darker behind the bot, 0.3 to 2.")],
              tips=["For <b>Picture</b>, put <code>.jpg</code> or <code>.png</code> files in the <code>Stage Pictures</code> folder (in the app's data folder) and they appear in the list.",
                    "<b>Use the stage set in Unity</b> (bottom of the page, after a change) returns to the delivered backdrop.",
                    "Light and dark colours follow Display › Appearance."])
    menu_page("lighting", "Lighting", "Menu › Maya › Lighting",
              "How the bot is lit. Changes show at once on the bot behind the page.",
              [("starts:LOOKS", "<b>LOOKS</b> - <b>Natural</b> (as delivered), <b>Soft</b> (gentle, flattering for faces), <b>Bright</b> (clear, like a window), <b>Dramatic</b> (light from one side, deep shadows), <b>Warm</b> (golden), <b>Cool</b> (clean daylight) and <b>Glow</b> (a bright halo from behind). Start here."),
               ("MAIN LIGHT", "<b>MAIN LIGHT</b> - <b>Brightness</b>, <b>Direction</b> (from the left or right), <b>Height</b> and <b>Warmth</b> (warm to cool)."),
               ("Brightness", "<b>Brightness</b> - how strong the main light is."),
               ("Warmth", "<b>Warmth</b> - cooler (bluish) to warmer (golden)."),
               ("OTHER LIGHTS", "<b>OTHER LIGHTS</b> - <b>Soft light</b> on the shadow side of the face, <b>Back light</b> round the edges, <b>Studio glow</b> (soft light from all round) and how dark the <b>Shadows</b> are."),
               ("Reset lighting", "<b>Reset lighting</b> - back to how it was delivered.")],
              tips=["Faces look best with <i>Soft</i> or <i>Natural</i>. <i>Dramatic</i> is strong - keep it for effect.", "Lighting is saved per bot, so each can have its own.",
                    "The brightness of the background is set in Backdrop."])

    menu_page("manage", "Copy, export, reset or delete", "Menu › Maya › Copy, export, reset or delete",
              "Look after the bot on screen. These are kept off the first page so nothing is tapped by mistake.",
              [("COPY", "<b>Make a copy of Maya</b> - a new bot with the same look, voices, personality, settings and documents. Then change what you like."),
               ("TAKE IT TO ANOTHER KIOSK", "<b>Export Maya to Desktop</b> - one <code>.altbot</code> file with the bot's look, voices, personality, settings, backdrop, documents and taught answers. On the other kiosk use <i>Import a bot…</i>. Plug in a USB drive and it appears here as another destination."),
               ("RESET", "<b>Reset Maya's settings</b> - puts voices, personality and conversation settings back to how they were delivered. Documents and the camera aren't touched."),
               ("DELETE", "<b>Delete</b> - only bots made here (New bot or a copy) can be deleted, after a second tap. The built-in six can't be.")],
              tips=["Export before deleting a bot, in case you change your mind.", "Deleted documents and taught answers go to the Recycle Bin."])

    # ---- VISITORS
    h2("Visitors")
    menu_page("language", "Listening & language", "Menu › VISITORS › Listening & language",
              "How visitors start talking, and which languages the bots hear and answer in. The same for every bot.",
              [("Listening", "<b>Listening</b> - <b>Tap the button</b> (best in busy, noisy places) or <b>Automatic</b> (it listens whenever the bot is waiting; a short pause ends the question)."),
               ("Listens for", "<b>Listens for</b> - <b>Auto</b> tells English and Hindi apart by itself. Pick one language if visitors only speak that: it is then never confused."),
               ("Replies in", "<b>Replies in</b> - <b>Same as asked</b> (a Hindi question gets a Hindi answer), always English or always Hindi.")],
              tips=["In a loud room, make Automatic need a louder voice: Advanced › Listening › <i>Automatic: how loud a voice must be</i>.",
                    "The background noise filter and other fine settings are in Advanced › Listening."],
              level=3)
    menu_page("learning", "Questions & answers", "Menu › VISITORS › Questions & answers",
              "What visitors asked, and where you teach the bots better answers. Nothing is learnt without you.",
              [("COULDN'T ANSWER", "<b>COULDN'T ANSWER</b> - questions about the bot's topics that its documents didn't answer (or answered only in part). Tap one to teach the right answer. <i>Asked 2×</i> shows how often."),
               ("RECENT ANSWERS", "<b>RECENT ANSWERS</b> - the last answers given. Tap one that was wrong to correct it."),
               ("Taught answers", "<b>Taught answers</b> - see, change or delete the answers you have taught."),
               ("Save conversations", "<b>Save conversations</b> - keep a log of questions and answers on this PC (on by default). Switch off to keep nothing."),
               ("Save a report", "<b>Save a report</b> - writes a readable summary and opens it."),
               ("Open the conversations folder", "<b>Open the conversations folder</b> - the raw log files."),
               ("starts:Export learning", "<b>Export learning to Desktop</b> - one zip with the conversations, taught answers and the report, to take to the development computer or another kiosk.")],
              tips=["A taught answer is used from the very next question.", "Weather, cricket and other off-topic questions are refused on purpose and are <b>not</b> listed here."],
              level=3)
    h4("Teaching an answer")
    teach_numbers = {"QUESTION": 1, "contains:SAID": 2, "THE RIGHT ANSWER": 3, "Teach this answer": 4, "Not worth teaching - hide it": 5}
    add('<div class="grid g2"><div class="cell">' + figure(shots, "teach", 70, width=900, small=True, numbers=teach_numbers,
        caption="<b>Teach an answer</b> - for a question it couldn't answer.") + '</div><div class="cell">' + figure(shots, "correct", 70, width=900, small=True,
        numbers=teach_numbers, caption="<b>Correct an answer</b> - for an answer that was wrong.") + '</div></div>')
    add(callout_list([
        ("QUESTION", "<b>QUESTION</b> - the question, which you can edit into the general form visitors would ask. Type it, or tap <b>Speak</b> to say it."),
        ("contains:SAID", "<b>MAYA SAID</b> (correcting only) - what the bot answered."),
        ("THE RIGHT ANSWER", "<b>THE RIGHT ANSWER</b> - write or say it the way the bot should say it, in one or two sentences."),
        ("Teach this answer", "<b>Teach this answer</b> - saves it. Maya and Ethan (or Pearl and Peter) both use it from the next question."),
        ("Not worth teaching - hide it", "<b>Not worth teaching - hide it</b> - removes a question from the list when it isn't useful (only for questions it couldn't answer).")],
        teach_numbers))
    p("Hindi questions are matched in English, so keep the question in English.")
    add('<div class="mp side" style="--figw:62mm">' + figure(shots, "taughtedit", 62, width=900, small=True) + '''<div class="text">
<h4>Changing a taught answer</h4><p>Tap an answer in <b>Taught answers</b> to change it. The picture shows an example.</p>
<ol class="co"><li><span class="n">1</span><b>QUESTION</b> and <span class="n">2</span><b>ANSWER</b> - edit either.</li>
<li><span class="n">3</span><b>Save</b> - keep the change.</li><li><span class="n">4</span><b>Forget this answer</b> - delete it.</li></ol>
<p>Each taught answer is a small text file named <i>Taught answer - …</i> in the bot's document folder, so you can also edit it there.</p></div></div>''')

    menu_page("report", "Report", "Menu › VISITORS › Report",
              "How busy the kiosk was and what people asked.",
              [("QUESTIONS", "<b>QUESTIONS</b> - today, the last 7 days, the last 30 days, and the share answered from the documents."),
               ("LAST 7 DAYS", "<b>LAST 7 DAYS</b> - questions per day, split into answered, not fully answered, small talk and open chat."),
               ("MOST ASKED", "<b>MOST ASKED</b> - the top questions, and which bot was asked."),
               ("COULDN'T ANSWER", "<b>COULDN'T ANSWER</b> - opens the list of questions to review."),
               ("EXPORT TO EXCEL", "<b>EXPORT TO EXCEL</b> - saves everything as one Excel file."),
               ("starts:Save to", "<b>Save to Desktop</b> - or onto a USB drive if one is plugged in.")],
              tips=["The Excel file covers the last 30 days: questions per day, per bot, the most asked, what it couldn't answer and every question with its answer.",
                    "The picture uses made-up example questions; yours show real ones."],
              level=3)

    # ---- DEVICES
    h2("Devices")
    p("The microphone, speaker and screen this PC uses. All three remember your choice, and fall back to the system default if the device is unplugged.")
    add('<div class="grid g3">' +
        '<div class="cell">' + figure(shots, "mics", 54, width=700, small=True, caption="<b>Microphone</b>") + '</div>' +
        '<div class="cell">' + figure(shots, "speakers", 54, width=700, small=True, caption="<b>Speaker</b>") + '</div>' +
        '<div class="cell">' + figure(shots, "screens", 54, width=700, small=True, caption="<b>Screen</b>") + '</div></div>')
    table(["Page", "What it does"], [
        ["Microphone", "<b>System default</b> (whatever Windows uses) or any microphone by name. The tick shows the one in use. A unplugged microphone falls back to the default."],
        ["Speaker", "Where the bot's voice comes out. Only this app's sound moves: other sounds stay where they are. Test it by tapping a voice in <i>Look &amp; voice</i>."],
        ["Screen", "Tap a screen to move the app there; it is remembered for the next start. With only one screen listed, the page says why: a second TV that is switched off, on another input, or that Windows is mirroring (<i>Duplicate</i>) isn't a separate screen. Press <span class='k'>Win</span> + <span class='k'>P</span>, choose <b>Extend</b>, then tap <b>Look again</b>. <b>Open Windows display settings</b> opens Windows' own page."]], "plain")
    note("tip", "Best microphone", "A headset or a directional microphone close to the visitor works far better than the PC's built-in one. Keep it out of the speaker's direction, so the bot doesn't hear itself.")

    # ---- DISPLAY
    h2("Display")
    p("Three switches on the first page, for every bot. They take effect at once.")
    table(["Setting", "What it does"], [
        ["Appearance", "<b>Light</b> or <b>Dark</b>. Dark (default) suits a dim venue; the stage backdrop follows it."],
        ["Show keyboard", "Adds a keyboard button next to the microphone, so visitors can type their question with an on-screen keyboard. Off by default."],
        ["Debug panel", "Timings and details for a developer: the question as heard, scores, timings of each step. <span class='k'>F1</span> also shows or hides it. Keep it off for visitors."]], "plain")

    # ---- ADVANCED
    h2("Advanced")
    p("Rarely needed. The AI models are already set up; change them only if you are told to.")
    menu_page("models", "AI models", "Menu › ADVANCED › AI models",
              "The AI parts that run on this PC. Changes are used from the next time the app starts.",
              [("BRAIN - LANGUAGE MODEL", "<b>BRAIN</b> - the language model that writes answers and translates. <b>Gemma 3 4B</b> was chosen after testing: the best Hindi and the fastest."),
               ("EARS - SPEECH RECOGNITION", "<b>EARS</b> - speech recognition (Whisper large-v3-turbo): turns what visitors say into text, English and Hindi."),
               ("DOCUMENT SEARCH - ANSWER CHECKER", "<b>ANSWER CHECKER</b> - after the search finds the closest passages, this re-reads the question with each and gives the brain the best. <b>On</b> is best; it adds about 0.1 second."),
               ("starts:VOICE - KOKORO", "<b>KOKORO</b> - the English voice engine. Always on; it also stands in for any voice engine turned off."),
               ("VEENA · NATURAL HINDI", "<b>VEENA</b> - the natural Hindi (and Indian English) voices. <b>Off</b> starts faster and frees graphics memory, but Hindi then sounds plainer."),
               ("ONLINE AI", "<b>ONLINE AI</b> - opens the Online AI page (next): let an online model write the answers instead of this PC."),
               ("Use the models set in the Inspector", "<b>Use the models set …</b> - goes back to the models the app was delivered with.")],
              tips=["Turning an engine off keeps it on disk but never loads it.", "To add a model file, tap <b>Add a … model</b>: it opens the folder to copy the file into."],
              level=3)
    menu_page("online", "Online AI", "Menu › ADVANCED › AI models › Online models & API keys",
              "An option for kiosks with internet: a stronger online model writes the answers (and reads Hindi and other languages better). "
              "Hearing, the voice, the document search and the topic checks still run on this PC.",
              [("THE BRAIN", "<b>THE BRAIN</b> - <b>Answers</b>: <b>On this PC</b> (default, fully offline) or <b>Online</b>. A line under it says what is happening now."),
               ("SERVICE", "<b>SERVICE</b> - <b>OpenAI</b>, <b>Claude</b>, <b>Gemini</b>, or <b>Other</b> (any server that speaks the OpenAI way: type its address)."),
               ("MODEL", "<b>MODEL</b> - the model's name. Claude starts with a small, fast one; for the others pick from the list."),
               ("starts:Choose from the service", "<b>Choose from the service's list</b> - asks the service which models your key can use. Tap one to choose it."),
               ("API KEY", "<b>API KEY</b> - paste the key from the service's website (the <b>Paste</b> button copies it from the clipboard), then tap <b>Save the key</b>."),
               ("Save the key", "<b>Save the key</b> - kept in Windows' secure store for this user, never in the app's files, exported bots or builds. <b>Remove the key</b> appears once one is saved."),
               ("Test the connection", "<b>Test the connection</b> - sends one tiny question and tells you plainly whether the key and model work, and how long it took.")],
              tips=["A small, fast model is plenty: the bot's answers are short and come from your documents.",
                    "If the service can't be reached or refuses, <b>that question is answered by this PC's model</b> and visitors see no error. After two failures in a row it uses this PC's model for a minute before trying again.",
                    "Each answer is charged by the service to your account (usually a fraction of a cent). Set a monthly spending limit on the service's website.",
                    "What is sent: the visitor's question, the matching pieces of the bot's documents and the bot's personality text. Nothing else leaves the PC."],
              level=3)
    menu_page("staff", "Staff sign-in", "Menu › ADVANCED › Staff sign-in",
              "Who can open the menu. Visitors can't, without a username and password.",
              [("WHO CAN OPEN THIS MENU", "<b>WHO CAN OPEN THIS MENU</b> - tap a person to change their password or remove them. <i>You</i> marks the person signed in."),
               ("Add a person…", "<b>Add a person…</b> - a name and password for each member of staff.")],
              tips=["<b>Change the delivered password before the kiosk goes public.</b> Add your own staff here, then the delivered one stops working.",
                    "Passwords are stored only as a secure fingerprint, never as text, and only on this PC.",
                    "Use at least 4 characters. The last person who can sign in can't be removed."],
              level=3, fig_mm=60)
    menu_page("staffadd", "Add a person", "Menu › ADVANCED › Staff sign-in › Add a person…",
              "Give someone their own sign-in.",
              [("NAME", "<b>NAME</b> - for example <i>reception</i>."),
               ("PASSWORD", "<b>PASSWORD</b> and <b>The same again</b> - typed on the keyboard only; passwords are never spoken."),
               ("Add", "<b>Add</b> - they can sign in straight away.")],
              level=4, fig_mm=60)

    h3("Fine-tuning pages", "Menu › ADVANCED")
    p("Seven pages hold the finer adjustments: <b>Display &amp; performance</b>, <b>Listening</b>, <b>Voice &amp; sound</b>, <b>Mouth &amp; lip-sync</b>, "
      "<b>Eyes</b>, <b>Body</b> and <b>Brain</b>. Every one lists its settings with a one-line explanation, and ends with <b>Reset this page</b> (back to the delivered values). "
      "A page showing <i>1 changed</i> beside it on the menu has something adjusted.")
    note("info", None, "Sliders move while you drag and are saved when you let go. All of these are for every bot, and apply at once unless a note says otherwise.")

    menu_page("tun_display", "Display & performance", "Menu › ADVANCED › Display & performance",
              "How the screen looks and how hard the graphics card works.",
              [("Text & button size", "<b>Text &amp; button size</b> (0.6× to 1.6×) - everything on screen, bigger or smaller."),
               ("Frame rate cap", "<b>Frame rate cap</b> (20 to 120) - keep <b>60</b>. Lower frees the graphics card for the AI; above 60 can make the Hindi voice break up."),
               ("Show the conversation text", "<b>Show the conversation text</b> - off: only the buttons show, the question and answer are just heard."),
               ("Clear the conversation after", "<b>Clear the conversation after</b> (0 to 120 s, default 6) - how soon the screen is ready for the next visitor. 0 = never."),
               ("Reset this page", "<b>Reset this page</b> - back to the delivered values.")],
              level=4)
    table(["Other settings on this page", "What it does"], [
        ["Frosted glass", "The blur behind the panels. <b>Auto</b> (full up to 1440p, lighter on bigger screens), full, or off (a solid panel - fastest)."],
        ["Loading screen stays after loading", "How long (0 to 3 s) the loading screen holds once ready."],
        ["Premium look (post-processing)", "A film-like colour grade, soft glow, darker corners and background blur. Off saves a little graphics power."],
        ["Background blur", "How out of focus the stage behind the bot is (0 = sharp)."],
        ["Smooth edges (anti-aliasing)", "Removes jagged edges on hair and outlines."],
        ["Fill light", "Soft light on the shadow side of the face. 0 = dramatic, 0.5 = even."],
        ["3D detail limit (million pixels)", "On a screen bigger than this (4K = 8.3) the 3D scene is drawn smaller and sharpened up to fit; text and buttons stay fully sharp. 4K drawn in full slowed answers by about half in a test."],
        ["Studio lighting / Studio light strength", "Lights the bot like a photo studio: soft light from all round and highlights in the eyes. Strength: higher is brighter and flatter."]], "plain")

    menu_page("tun_listening", "Listening", "Menu › ADVANCED › Listening",
              "How the microphone decides when someone is talking.",
              [("Stop listening when you stop talking", "<b>Stop listening when you stop talking</b> - hands-free: after a pause it stops by itself, so there is no second tap."),
               ("Background noise filter", "<b>Background noise filter</b> - cuts low rumble (air conditioning, traffic) and ignores recordings without a clear voice."),
               ("starts:Automatic: how loud", "<b>Automatic: how loud a voice must be</b> (1.5× to 8×) - how much louder than the room a voice must be to start a question. Higher = only people close to the screen."),
               ("Reset this page", "<b>Reset this page</b>.")],
              level=4)
    table(["Other settings on this page", "What it does"], [
        ["Pause before it stops", "With <i>Stop listening when you stop talking</i> on: how long a silence (0.5 to 5 s) ends the question."],
        ["Longest question", "Listening stops after this long (5 to 60 s), whatever happens."],
        ["Voice must stand out from the noise by (dB)", "Noise filter: how much louder than the room the voice must be. Higher ignores more, including quiet speakers."],
        ["Automatic: pause that ends a question", "In Automatic mode, how long a pause (0.5 to 3 s) ends the question."]], "plain")

    add('<div class="grid g2"><div class="cell">' + figure(shots, "tun_voice", 62, width=900, small=True, caption="<b>Voice &amp; sound</b>") + '</div>'
        '<div class="cell">' + figure(shots, "tun_brain", 62, width=900, small=True, caption="<b>Brain</b>") + '</div></div>')
    h4("Voice & sound")
    table(["Setting", "What it does"], [
        ["Voice volume (0 to 100%)", "The app's own volume, on top of Windows' volume."],
        ["Voice buffer before each sentence (0.3 to 3 s)", "Veena voices: audio collected before a sentence starts. Raise it if the voice breaks up on a busy computer."],
        ["Save spoken sentences for review", "Saves each spoken sentence as a sound file in the <code>SpeechReview</code> folder, so a developer can check the voice. <b>Off by default</b> and best left off on a kiosk: the folder grows with every answer. Switch it on only when a developer asks."]], "plain")
    h4("Brain")
    table(["Setting", "What it does"], [
        ["Second chance for near-miss questions (0 to 0.3)", "Knowledge bots: a customer question scoring this close below <i>Topic strictness</i> still gets checked against the documents."],
        ["New visitor after (20 to 600 s)", "After this long without a question, follow-ups (<i>\"does it…\"</i>) and open chat memory start fresh."],
        ["Taught answers: how close a question must be (0.3 to 0.95)", "Open chat bots: how closely a question must match a taught answer to use it."]], "plain")

    add('<div class="grid g3">' +
        '<div class="cell">' + figure(shots, "tun_mouth", 54, width=800, small=True, caption="<b>Mouth &amp; lip-sync</b>") + '</div>' +
        '<div class="cell">' + figure(shots, "tun_eyes", 54, width=800, small=True, caption="<b>Eyes</b>") + '</div>' +
        '<div class="cell">' + figure(shots, "tun_body", 54, width=800, small=True, caption="<b>Body</b>") + '</div></div>')
    table(["Page", "Setting", "What it does"], [
        ["Mouth &amp; lip-sync", "Lip movement (0 to 2×)", "How much the lips and mouth shapes move, for every character."],
        ["", "Jaw opening (0 to 2×)", "How wide the jaw opens."],
        ["", "Mouth speed (0.5 to 2×)", "Higher follows the voice more sharply; lower is smoother."],
        ["", "Mouth ahead of the voice (0 to 0.2 s)", "The mouth is read ahead of the audio so it moves with it; about 0.07 s looks right."],
        ["Eyes", "Eyelids", "Added to every character's resting eyelids: higher is more relaxed, lower is wider open."],
        ["", "Blinking (0 to 2×) and Double blinks", "How often it blinks (0 = never) and how often it blinks twice."],
        ["", "Small eye movements, Glances away", "The tiny flicks real eyes make (0 = a fixed stare), and how far it now and then looks aside (0 = always looks at you)."],
        ["", "Eyelids follow the gaze", "How strongly the eyelids follow where the eyes look."],
        ["Body", "Body motion speed (0.5 to 1.5×)", "All idle and talking motions."],
        ["", "Keep the talking pose between sentences (0 to 2 s)", "Longer stays in the talking motion through pauses; shorter drops back to idle sooner."],
        ["", "Smoothness between motions", "How slowly one motion blends into the next."]], "plain")

    h3("Sign out and Close the app", "Menu › bottom of the first page")
    p("<b>Sign out (name)</b> ends your staff session at once and closes the menu. <b>Close the app</b> asks for a second tap (it says <i>Tap again to close the app</i>) and then quits. "
      "If you don't tap again within 4 seconds it goes back to normal.")

    # ------------------------------------------------------------------ 5
    chapter(5, "How do I…?", "The jobs staff do most, step by step. Menu paths read: <i>Menu › the page › the row</i>.")
    recipes = [
        ("Change which bot is on screen", ["Tap the menu button and sign in.", "In <b>BOTS</b>, tap the bot's name (the tick moves).", "Close the menu with the <b>×</b>. The new bot is on screen."]),
        ("Add a document so a bot can answer new questions",
         ["Put the file on the PC or a USB drive (PDF, Word, PowerPoint or text).", "Menu › the bot (e.g. Maya) › <b>Documents</b> › <b>Add documents…</b>.", "Pick the file(s) in the window and open them. Leave <b>Same for Ethan</b> on, so both bots get it.",
          "Wait about a minute, then ask a question from the new document to check."]),
        ("Remove a document", ["Menu › the bot › <b>Documents</b> › tap the file.", "Tap <b>Remove from …</b> and tap again to confirm. It goes to the Recycle Bin."]),
        ("Teach a bot a better answer", ["Menu › <b>Questions &amp; answers</b>.", "Under <b>COULDN'T ANSWER</b>, tap the question. (For a wrong answer, tap it under <b>RECENT ANSWERS</b>.)", "Edit the question if you like, then type or say <b>THE RIGHT ANSWER</b>.", "Tap <b>Teach this answer</b>. It is used from the next question."]),
        ("Change a bot's voice", ["Menu › the bot › <b>Look &amp; voice</b>.", "Tap <b>English voice</b> (or <b>Hindi voice</b>), then tap voices to hear them. The tick shows your choice.", "Keep both voices the same gender. Go back."]),
        ("Make a bot for another team or product", ["Menu › BOTS › <b>New bot…</b>.", "Type the name, pick a look and <b>Knowledge only</b>, then <b>Create bot</b>.", "On the Documents page that opens, <b>Add documents…</b>.", "Check its <b>Personality</b> (topics, who it is) and <b>Look &amp; voice</b>."]),
        ("Move a bot to another kiosk", ["On the first kiosk: Menu › the bot › <b>Copy, export, reset or delete</b> › <b>Export … to</b> your USB drive.", "On the other kiosk: Menu › BOTS › <b>Import a bot…</b>, pick the <code>.altbot</code> file."]),
        ("Get the weekly report in Excel", ["Menu › <b>Report</b>.", "Tap <b>Save to Desktop</b> (or to the USB drive shown).", "Open the Excel file: questions per day, per bot, the most asked and every answer."]),
        ("Make the bot look better on screen", ["Menu › the bot › <b>Camera</b>: tap <i>Relaxed</i> or <i>Chest up</i> on a tall screen, then fine-tune.", "<b>Lighting</b>: try <i>Soft</i> for faces.", "<b>Backdrop</b>: pick <i>Studio</i> or a picture, and set its brightness."]),
        ("Make it work in a noisy place", ["Menu › <b>Listening &amp; language</b> › <b>Tap the button</b>.", "Use a headset or directional microphone (Menu › <b>Microphone</b>).", "Advanced › <b>Listening</b>: keep the <i>Background noise filter</i> on; raise <i>Voice must stand out from the noise</i> a little if noise is taken for questions."]),
        ("Set up sign-ins for the team", ["Menu › Advanced › <b>Staff sign-in</b> › <b>Add a person…</b>.", "Type a name and a password twice, tap <b>Add</b>.", "Repeat for each person. Add yourself first; the delivered sign-in then stops working."]),
        ("Make a visitor's question disappear quicker (or slower)", ["Menu › Advanced › <b>Display &amp; performance</b>.", "Change <b>Clear the conversation after</b> (6 seconds is the default)."]),
        ("Save disk space", ["Menu › Advanced › <b>Voice &amp; sound</b>: check that <b>Save spoken sentences for review</b> is <b>off</b> (it is by default).", "Delete old folders in <code>SpeechReview</code> next to the app, if any (an earlier version saved them).", "Old conversation logs are small; the AI models are the big files and shouldn't be deleted."]),
    ]
    for title, st in recipes:
        add(f'<div class="nobreak"><h3{token(3, title)}>{title}</h3><ol class="steps">' + "".join(f"<li>{s}</li>" for s in st) + "</ol></div>")

    # ------------------------------------------------------------------ 6
    chapter(6, "Looking after the kiosk", "Small habits that keep it running well.")
    h2("Every day")
    ul(["Check the screen shows the welcome line and the microphone button says <b>Tap to talk</b> (or <i>Just start talking</i>).", "Say a test question out loud and check the answer and the voice.",
        "Make sure the menu is <b>signed out</b>."])
    h2("Every week")
    ul(["Open Menu › <b>Questions &amp; answers</b> and look at <b>COULDN'T ANSWER</b>. Teach answers to the questions that matter.", "Open Menu › <b>Report</b> and save the Excel file.",
        "Look at <code>watchdog.log</code> next to the app: many restarts mean something needs looking at."])
    h2("Backups")
    p("Copy these to another drive now and then (the whole app folder is about 21 GB):")
    table(["What", "Why"], [
        ["The app folder", "Everything the app needs. If the PC fails, copy it back to another PC."],
        ["<code>AltcoreBot_Data\\StreamingAssets\\Knowledge</code>", "The documents and the answers you taught."],
        ["<code>AltcoreBot_Data\\StreamingAssets\\Bots</code>", "Bots made in the app."],
        ["Exported bots (<code>.altbot</code>)", "A portable copy of one bot, including its settings and documents."]], "plain")
    note("info", None, "The menu settings (voices, camera, sign-ins) live in the Windows registry of the signed-in user, not in the folder. A copied folder on a new PC starts with the settings it was delivered with. Export a bot to carry its own settings.")
    h2("Windows and drivers")
    ul(["Keep the NVIDIA driver current, but update it when the kiosk is not in use and restart afterwards.", "Set Windows <b>Active hours</b> so updates don't restart the PC during opening hours.",
        "Turn off sleep and screen timeout. Keep other graphics-heavy programs closed: they slow the AI and can make the Hindi voice break up."])

    # ------------------------------------------------------------------ 7
    chapter(7, "Troubleshooting", "Find the symptom, try the fix. If it persists, note the exact message and contact the person who installed the kiosk.")
    h2("CHECK THIS PC")
    p("At the top of the menu (and on the loading screen) a <b>CHECK THIS PC</b> box lists anything the computer is missing, in plain words:")
    table(["Message", "What to do"], [
        ["No microphone found - plug one in.", "Plug in a microphone. It is found again when you reopen the menu."],
        ["The chosen microphone … isn't connected - the default microphone is used.", "Plug it back in, or choose another in Menu › Microphone."],
        ["No speaker or headphones found.", "Connect speakers, headphones or a TV with sound."],
        ["The chosen speaker isn't connected.", "Reconnect it or choose another in Menu › Speaker."],
        ["The graphics card isn't NVIDIA.", "The AI needs an NVIDIA card. Use a supported PC."],
        ["The graphics card has less than 12 GB of memory.", "Works, but answers are slower and Indian voices may break up. In Advanced › AI models you can turn off unused voice engines."],
        ["This PC has less than 16 GB of memory.", "Add RAM if you can."]], "plain")

    h2("Problems and fixes")
    rows = [
        ("It says <b>Couldn't start</b> on the loading screen", "Read the reason under it. Close the app, restart the PC and try again. Make sure the folder is not in <i>Program Files</i>, there is free disk space, the NVIDIA driver is installed, and the antivirus hasn't quarantined files."),
        ("The loading screen never ends", "The first start takes a minute or two. If it passes 5 minutes, close it and start again with <code>Start AltcoreBot.cmd</code>. Check other graphics-heavy programs aren't running."),
        ("It doesn't react when people speak", "In <b>Tap the button</b> mode people must tap the microphone first. In <b>Automatic</b> mode the voice may be too quiet for the noise filter: Advanced › Listening, lower <i>Automatic: how loud a voice must be</i>. Check the microphone is the right one (Menu › Microphone)."),
        ("\"Didn't catch that\" all the time", "Closer to the microphone, a quieter spot, a headset microphone. In Advanced › Listening, lower <i>Voice must stand out from the noise</i> a little. Make sure the bot isn't speaking while you talk."),
        ("A room's noise is taken for questions", "Use <b>Tap the button</b> mode. Keep the <i>Background noise filter</i> on and raise <i>Voice must stand out from the noise</i>."),
        ("It heard the wrong words", "Speak a little slower. Company names can be misheard: ask again, or teach the answer for the question in the form visitors say it."),
        ("\"I can't help with that one\" for a good question", "It isn't in the documents. Add the document, or teach the answer (Menu › Questions &amp; answers). If the documents do cover it, lower <b>Topic strictness</b> a little in Menu › Conversation."),
        ("An answer is wrong or made up", "Raise <b>Answer checking</b> a little. Teach the right answer (Recent answers › tap it). Make sure the bot is on <b>Knowledge only</b>."),
        ("Nothing new from a document I just added", "Give it a minute (longer for big files). A scanned PDF with only page pictures has no text: use the Word original. Hindi PDFs often read back scrambled: use Word or text."),
        ("No sound", "Windows volume and the right speaker (Menu › Speaker). Advanced › Voice &amp; sound: <i>Voice volume</i>. If the TV has several outputs, pick the right one. Wait: the voice loads while the loading screen is showing."),
        ("The Hindi voice breaks up or stutters", "Keep <b>Frame rate cap</b> at 60. Close other programs that use the graphics card. In Advanced › Voice &amp; sound raise <i>Voice buffer before each sentence</i>. On a 4K screen lower <i>3D detail limit</i>. As a last step, turn off Veena (Advanced › AI models): Hindi then uses the plainer voices."),
        ("Answers are slow", "Other programs using the graphics card, a very high resolution (4K), or low graphics memory. See the performance settings in Advanced › Display &amp; performance."),
        ("The bot looked folded or odd for a moment after switching", "It is being posed behind the scenes. A second later it appears normally. Switch once and wait."),
        ("The menu won't open", "It asks for a sign-in. After 5 wrong tries it pauses for 30 seconds. If the password is forgotten, ask the person who installed the kiosk."),
        ("The screen is the wrong way round or shows on the wrong monitor", "Windows display settings: choose <b>Portrait</b>. With several screens use Menu › Screen."),
        ("Menu › Screen lists only one screen although two TVs are connected", "Windows is showing them as one picture (<i>Duplicate</i>), or one TV is off or on another input. Press <span class='k'>Win</span> + <span class='k'>P</span> and choose <b>Extend</b>, switch the other TV on, then tap <b>Look again</b> on the Screen page."),
        ("The app closed by itself", "Started with <code>Start AltcoreBot.cmd</code>, it restarts by itself (within seconds after a crash, about two minutes after a freeze). <code>watchdog.log</code> says why. If it stopped 5 times in 10 minutes it gives up: restart it and tell the installer."),
        ("\"The voice engine keeps stopping - restart the app\"", "The voice server stopped 3 times in 10 minutes. Close the app (Menu › Close the app) and start it again. If it repeats, tell the installer."),
        ("Online AI: \"didn't accept this API key\", \"doesn't know this model\", \"too many requests\" or \"couldn't reach\"", "Open Menu › Advanced › AI models › Online models &amp; API keys and tap <b>Test the connection</b>: it names the problem. <i>Didn't accept the key</i> - paste the key again, or the account may not be allowed to use that model. <i>Doesn't know this model</i> - tap <b>Choose from the service's list</b>. <i>Too many requests</i> - the account is out of credit or over its limit. <i>Couldn't reach</i> - check the internet. Meanwhile this PC's model keeps answering."),
        ("The disk is filling up", "Check that <b>Save spoken sentences for review</b> (Advanced › Voice &amp; sound) is off, and delete old folders in <code>SpeechReview</code> (an earlier version saved them). Conversation logs are small."),
    ]
    table(["Problem", "What to try"], [[a, b] for a, b in rows])
    note("info", "When you ask for help", "Say what the screen says word for word, which bot was on, what you did just before, and attach <b>Menu › Questions &amp; answers › Save a report</b> and the file <code>Player.log</code> from <code>C:\\Users\\&lt;user&gt;\\AppData\\LocalLow\\Altcore\\AltcoreBot</code>.")

    # ------------------------------------------------------------------ 8
    chapter(8, "Reference", "Quick facts to look things up.")
    h2("Shortcuts")
    table(["Key", "What it does"], [["<span class='k'>Space</span>", "Hold to talk (keyboard attached). Release to ask."], ["<span class='k'>Esc</span>", "Closes the menu or the sign-in card."], ["<span class='k'>F1</span>", "Shows or hides the debug panel."]], "plain")
    h2("The AI under the hood")
    table(["Part", "What it is"], [
        ["Brain", "Gemma 3 4B (Q4_K_M), run through llama.cpp on the NVIDIA card - or, if Online AI is switched on, an online model (OpenAI, Claude, Gemini or another service)."],
        ["Ears", "Whisper large-v3-turbo. It decides English or Hindi from the voice, then transcribes."],
        ["Document search", "bge-m3, a multilingual search model, plus a reranker (bge-reranker-v2-m3) that reads the question with each passage."],
        ["Voices", "Kokoro (English, and plainer Hindi) and Veena (natural Hindi and Indian English)."],
        ["Lip-sync", "uLipSync, listening to the voice live, so the mouth is never pre-recorded."],
        ["Characters", "Reallusion Character Creator 4 models with face shapes, gaze and blinking."]], "plain")
    h2("Glossary")
    terms = [("Bot", "One character with its own name, look, voices, personality and documents."), ("Knowledge only", "A bot that answers only from its documents."), ("Open chat", "A bot that talks about anything from its own knowledge."),
             ("Document", "A PDF, Word, PowerPoint or text file a Knowledge only bot learns from."), ("Passage", "A small piece of a document found for a question."),
             ("Topic strictness", "How closely a question must match the documents before the bot answers."), ("Answer checking", "How closely an answer must match the documents before it is spoken."),
             ("Taught answer", "An answer staff wrote for a question. Used from the next question."), ("Backdrop / Stage", "What is behind the bot: a studio look, gradient, picture or colour."),
             ("Kiosk", "The screen, PC and microphone set up for visitors."), ("Veena", "The natural Indian voices (Hindi and Indian English)."), ("Watchdog", "The small script that restarts the app if it crashes or freezes.")]
    table(["Word", "Meaning"], [[a, b] for a, b in terms], "plain")
    h2("Quick card")
    add('<div class="grid g2"><div class="cell"><h4>Visitors</h4><ol class="steps"><li>Tap the microphone.</li><li>Ask in English or Hindi.</li><li>Tap again (or stop talking).</li><li>Listen. Tap to stop it.</li></ol></div>'
        '<div class="cell"><h4>Staff</h4><ol class="steps"><li>Tap the menu button, sign in.</li><li>Pick a bot, or open one of its pages.</li><li>Change what you need. It saves itself.</li><li>Sign out.</li></ol></div></div>')
    note("ok", None, "You can read a guide to any menu page on the kiosk itself: tap the <b>?</b> at the top of the page.")


# =============================================================================================== BUILD

def toc_html(pages=None):
    out = ['<h1 class="chapter" style="break-before:page"><small>Contents</small>Contents</h1><div class="toc">']
    for i, (level, title, key) in enumerate(toc):
        pg = pages.get(i, "") if pages else "00"
        out.append(f'<div class="l{level}"><a href="#h{i}">{title}</a><span class="dots"></span><a class="pg" href="#h{i}">{pg}</a></div>')
    out.append("</div>")
    return "".join(out)


def main():
    cover()
    contents_placeholder()
    content()
    html_body = "".join(body)
    OUT.mkdir(exist_ok=True)
    pdf = OUT / "AltcoreBot-Staff-Manual.pdf"

    def write(pages):
        (OUT / "manual.html").write_text(page_html(html_body.replace("@@TOC@@", toc_html(pages))), encoding="utf-8")

    write(None)
    print_pdf(OUT / "manual.html", pdf)
    texts = pdf_pages_text(pdf)
    # Inter draws "(" next to a capital as a special glyph that PDF text extraction reports as a private-use character.
    norm = lambda s: re.sub(r"\s+", " ", re.sub(r"[-]", "(", s)).strip().lower()
    lines = [[norm(l) for l in t.split("\n")] for t in texts]
    # The first chapter's page ("Part 1") - the contents pages before it list every heading too.
    start = next((i for i, ls in enumerate(lines) if "part 1" in ls), 0)
    pages, at, missing = {}, start, []
    for n, (level, title, key) in enumerate(toc):
        k = norm(key)
        hit = next((i for i in range(at, len(lines)) if k in lines[i]), None)
        if hit is None:
            hit = next((i for i in range(at, len(lines)) if any(l.startswith(k) for l in lines[i])), None)
        if hit is None:      # a heading that wrapped onto two lines
            hit = next((i for i in range(at, len(lines)) if k in " ".join(lines[i])), None)
        if hit is None:
            missing.append(key)
            continue
        pages[n] = str(hit + 1)
        at = hit
    print(f"pass 1: {len(texts)} pages, {len(pages)}/{len(toc)} headings found" + (f", missing: {missing[:6]}" if missing else ""))
    write(pages)
    print_pdf(OUT / "manual.html", pdf)

    # Bookmarks (the PDF viewer's side panel) and the document's properties.
    from pypdf import PdfReader, PdfWriter
    writer = PdfWriter(clone_from=PdfReader(str(pdf)))
    writer.add_metadata({"/Title": "AltcoreBot - Staff manual", "/Author": "Altcore", "/Creator": "Altcore", "/Producer": "Altcore",
                         "/Subject": "How to run, use and look after the AltcoreBot offline voice assistant"})
    parents = {}
    for n, (level, title, key) in enumerate(toc):
        if n not in pages:
            continue
        parents[level] = writer.add_outline_item(title, int(pages[n]) - 1, parent=parents.get(level - 1) if level > 1 else None)
    writer.page_mode = "/UseOutlines"
    tmp = pdf.with_suffix(".tmp.pdf")
    with open(tmp, "wb") as f:
        writer.write(f)
    tmp.replace(pdf)
    print("pass 2:", len(pdf_pages_text(pdf)), "pages ->", pdf, f"({pdf.stat().st_size / 1e6:.1f} MB)")


if __name__ == "__main__":
    main()
