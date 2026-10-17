# -*- coding: utf-8 -*-
"""
VB6 / COM / Win32 Library Quick-Access Query Tool
Allows instant CLI search and lookup across all books, chapters, COM typelibs,
runtime symbols, and code samples.

Usage:
  python query_library.py search <keyword>
  python query_library.py topic <topic_key>
  python query_library.py book <curland|mckinney|balena|appleman_win32|appleman_com|don_box|lhotka>
  python query_library.py target <compiler|ide|runtime|api64>
  python query_library.py com <class_or_method_name>
  python query_library.py code <class_or_method_name>
  python query_library.py stats
"""

import os
import sys
import json
import re

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
DB_FILE = os.path.join(SCRIPT_DIR, "VB6_LIBRARY_DATABASE.json")
SYMBOLS_FILE = os.path.join(SCRIPT_DIR, "TYPELIB_AND_RUNTIME_SYMBOLS.json")
CODE_FILE = os.path.join(SCRIPT_DIR, "CODE_SAMPLES_INDEX.json")

def load_json(path):
    if not os.path.exists(path):
        print(f"Error: Database file not found: {path}")
        sys.exit(1)
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)

def cmd_stats():
    db = load_json(DB_FILE)
    symbols = load_json(SYMBOLS_FILE)
    code = load_json(CODE_FILE)

    print("=" * 70)
    print("  VB6 / COM / WIN32 QUICK-ACCESS KNOWLEDGE BASE STATISTICS")
    print("=" * 70)
    print(f"Total Reference Volumes / Books: {len(db['books'])}")
    total_pages = sum(b.get('pages', 0) for b in db['books'])
    print(f"Total Documented Pages:         {total_pages:,}")
    print(f"Core Indexed Topics:            {len(db['core_topics_cross_index'])}")
    print(f"msvbvm60 Runtime Components:    {symbols['msvbvm60_runtime']['components_count']}")
    print(f"msvbvm60 Enums Defined:         {len(symbols['msvbvm60_runtime']['enums'])}")
    print(f"VB6.OLB Controls & Interfaces:  {symbols['vb6_controls_typelib']['components_count']}")
    print(f"Rockford Lhotka Code Files:     {code['summary']['total_files']}")
    print(f"  - Class Modules (.cls):       {code['summary']['class_modules_cls']}")
    print(f"  - Form Modules (.frm):        {code['summary']['forms_frm']}")
    print(f"  - Standard Modules (.bas):    {code['summary']['standard_modules_bas']}")
    print(f"  - Project Files (.vbp):       {code['summary']['projects_vbp']}")
    print(f"  - Project Groups (.vbg):      {code['summary']['project_groups_vbg']}")
    print("=" * 70)

def cmd_search(term):
    db = load_json(DB_FILE)
    term_low = term.lower()
    print(f"=== SEARCH RESULTS FOR: '{term}' ===\n")

    # Search in books & chapters
    book_hits = 0
    for b in db["books"]:
        matched_chaps = []
        for ch in b.get("chapters", []):
            title_match = term_low in ch["title"].lower()
            topic_match = any(term_low in t.lower() for t in ch.get("topics", []))
            if title_match or topic_match:
                matched_chaps.append(ch)

        summary_match = term_low in b.get("summary", "").lower()
        if matched_chaps or summary_match:
            book_hits += 1
            print(f"[BOOK] {b['title']} (by {b['author']})")
            print(f"       File: {b['file']} ({b['pages']} pages)")
            for ch in matched_chaps:
                print(f"       -> Ch {ch['number']}: {ch['title']} (p. {ch['page']})")
                print(f"          Topics: {', '.join(ch.get('topics', []))}")
            print()

    # Search in topics
    topic_hits = 0
    for tk, tdata in db["core_topics_cross_index"].items():
        if term_low in tk or term_low in tdata["title"].lower() or term_low in tdata["description"].lower():
            topic_hits += 1
            print(f"[TOPIC] {tdata['title']} (key: {tk})")
            print(f"        {tdata['description']}")
            print(f"        Compiler: {tdata.get('compiler_relevance', '')}")
            print(f"        Runtime:  {tdata.get('runtime_relevance', '')}")
            print()

    print(f"Total matching books: {book_hits}, matching topics: {topic_hits}")

def cmd_topic(topic_key):
    db = load_json(DB_FILE)
    topics = db["core_topics_cross_index"]
    # exact or substring match
    matched = None
    for k, v in topics.items():
        if k.lower() == topic_key.lower() or topic_key.lower() in k.lower():
            matched = (k, v)
            break

    if not matched:
        print(f"Topic '{topic_key}' not found.")
        print("Available topics:")
        for k in sorted(topics.keys()):
            print(f"  - {k} ({topics[k]['title']})")
        return

    k, data = matched
    print("=" * 70)
    print(f"TOPIC: {data['title']} (Key: {k})")
    print("=" * 70)
    print(f"Description: {data['description']}\n")
    print("BOOK REFERENCES:")
    for ref in data.get("references", []):
        print(f"  * Book: {ref['book'].upper()} | Chapter {ref['chapter']} (Pages {ref['pages']})")
        print(f"    Notes: {ref['notes']}")
    print()
    print(f"COMPILER x64 RELEVANCE:\n  {data.get('compiler_relevance', 'N/A')}\n")
    print(f"RUNTIME x64 RELEVANCE:\n  {data.get('runtime_relevance', 'N/A')}\n")

def cmd_book(book_id):
    db = load_json(DB_FILE)
    b = next((x for x in db["books"] if x["id"].lower() == book_id.lower()), None)
    if not b:
        print(f"Book '{book_id}' not found.")
        print("Available book IDs:")
        for x in db["books"]:
            print(f"  - {x['id']}: {x['title']}")
        return

    print("=" * 70)
    print(f"{b['title']}")
    print(f"Author: {b['author']} | Publisher: {b['publisher']} ({b['year']}) | Pages: {b['pages']}")
    print(f"Importance: {b.get('importance', 'Standard')}")
    print(f"File: {b['file']}")
    print("=" * 70)
    print(f"Summary:\n{b['summary']}\n")
    print("RELEVANCE BY SUBSYSTEM:")
    for sub, rel in b.get("relevance", {}).items():
        print(f"  [{sub.upper()}]: {rel}")
    print("\nCHAPTERS:")
    for ch in b.get("chapters", []):
        topics_str = f" - Topics: {', '.join(ch['topics'])}" if ch.get('topics') else ""
        print(f"  Ch {ch['number']:2d}: {ch['title']:<45} (p. {ch['page']:4d}){topics_str}")

def cmd_target(target_name):
    db = load_json(DB_FILE)
    t = target_name.lower()
    mapping = {
        "compiler": "compiler_x64",
        "compiler_x64": "compiler_x64",
        "ide": "ide_x64",
        "ide_x64": "ide_x64",
        "runtime": "runtime_x64",
        "runtime_x64": "runtime_x64",
        "api64": "win32_api_x64",
        "win32_api_x64": "win32_api_x64"
    }
    key = mapping.get(t)
    if not key:
        print("Invalid target. Choose from: compiler, ide, runtime, api64")
        return

    print("=" * 70)
    print(f"RESOURCES AND BLUEPRINTS FOR: {key.upper()}")
    print("=" * 70)
    for b in db["books"]:
        rel = b.get("relevance", {}).get(key)
        if rel:
            print(f"[BOOK] {b['title']} (by {b['author']})")
            print(f"       File: {b['file']}")
            print(f"       Guidance: {rel}\n")

    print("RELEVANT TOPICS:")
    for tk, tdata in db["core_topics_cross_index"].items():
        field = "compiler_relevance" if "compiler" in key else ("runtime_relevance" if "runtime" in key else None)
        if field and tdata.get(field):
            print(f"  * {tdata['title']}: {tdata[field]}")

def cmd_com(query):
    symbols = load_json(SYMBOLS_FILE)
    q = query.lower()
    print(f"=== SEARCHING COM TYPELIBS FOR: '{query}' ===\n")

    # Search in msvbvm60 components
    msvbvm_matches = [c for c in symbols["msvbvm60_runtime"]["components"] if q in c["name"].lower() or q in c["guid"].lower()]
    if msvbvm_matches:
        print(f"[msvbvm60.dll Runtime] Found {len(msvbvm_matches)} matches:")
        for c in msvbvm_matches:
            print(f"  - {c['name']:<25} | GUID: {c['guid']}")
        print()

    # Search in VB6.OLB components
    vb_matches = [c for c in symbols["vb6_controls_typelib"]["components"] if q in c["name"].lower() or q in c["guid"].lower()]
    if vb_matches:
        print(f"[VB6.OLB Controls] Found {len(vb_matches)} matches:")
        for c in vb_matches:
            print(f"  - {c['name']:<25} | GUID: {c['guid']}")
        print()

    # Search in VB6 control interface methods
    methods_hits = []
    for iface, methods in symbols["vb6_controls_typelib"]["interfaces_methods"].items():
        for m in methods:
            if q in m["name"].lower():
                methods_hits.append((iface, m["name"], m["args"]))

    if methods_hits:
        print(f"[Control Methods/Properties] Found {len(methods_hits)} occurrences:")
        for iface, mname, margs in methods_hits[:15]:
            print(f"  - Interface {iface:<20}: {mname}({margs})")
        if len(methods_hits) > 15:
            print(f"  ... and {len(methods_hits) - 15} more.")

def cmd_code(query):
    code = load_json(CODE_FILE)
    q = query.lower()
    print(f"=== SEARCHING ROCKFORD LHOTKA CODEBASE FOR: '{query}' ===\n")

    matches = []
    for cls in code["classes"]:
        class_match = q in cls["class_name"].lower()
        method_match = [m for m in cls.get("methods", []) if q in m.lower()]
        if class_match or method_match:
            matches.append((cls, method_match))

    if matches:
        print(f"Found {len(matches)} matching classes:")
        for cls, mm in matches[:20]:
            print(f"  * Class: {cls['class_name']} ({cls['file']})")
            print(f"    Path:  {cls['relative_path']}")
            if mm:
                print(f"    Matching Methods: {', '.join(mm[:5])}")
            if cls.get("implements"):
                print(f"    Implements:       {', '.join(cls['implements'])}")
            print()
    else:
        print("No matching classes found.")

def main():
    if len(sys.argv) < 2:
        print(__doc__)
        cmd_stats()
        return

    cmd = sys.argv[1].lower()
    arg = sys.argv[2] if len(sys.argv) > 2 else ""

    if cmd == "stats":
        cmd_stats()
    elif cmd == "search":
        if not arg:
            print("Please provide a search keyword.")
            return
        cmd_search(arg)
    elif cmd == "topic":
        if not arg:
            print("Please provide a topic key.")
            return
        cmd_topic(arg)
    elif cmd == "book":
        if not arg:
            print("Please provide a book ID.")
            return
        cmd_book(arg)
    elif cmd == "target":
        if not arg:
            print("Please provide a target (compiler, ide, runtime, api64).")
            return
        cmd_target(arg)
    elif cmd == "com":
        if not arg:
            print("Please provide a COM class, method, or GUID.")
            return
        cmd_com(arg)
    elif cmd == "code":
        if not arg:
            print("Please provide a class name or method name.")
            return
        cmd_code(arg)
    else:
        print(f"Unknown command: {cmd}")
        print(__doc__)

if __name__ == "__main__":
    main()
