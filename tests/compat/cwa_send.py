"""Send one message exactly the way Calibre-Web-Automated does (cps/tasks/mail.py, CWA v4.0.8).

Stdlib only, so it runs on a CI runner and inside the CWA container. Prints what CWA would show the
user and exits 0 on success, 2 on an SMTP error, 3 on a socket error.
"""

import argparse
import io
import mimetypes
import smtplib
import socket
import sys
import zipfile
from email.generator import Generator
from email.message import EmailMessage
from email.utils import formatdate, make_msgid
from io import StringIO


def sample_book(name: str, size: int) -> bytes:
    """Minimal content that passes Paper Route's file-type check for the given name."""
    if name.lower().endswith(".epub"):
        buffer = io.BytesIO()
        with zipfile.ZipFile(buffer, "w") as epub:
            epub.writestr(zipfile.ZipInfo("mimetype"), "application/epub+zip")
            epub.writestr("content.opf", "<package/>")
        return buffer.getvalue()
    data = bytearray(size)
    data[60:68] = b"BOOKMOBI"  # MOBI/AZW3 type marker
    return bytes(data)


def build_message(mail_from: str, recipient: str, attachment: str | None, data: bytes) -> EmailMessage:
    # Mirrors TaskEmail.prepare_message
    message = EmailMessage()
    message["From"] = mail_from
    message["To"] = recipient
    message["Subject"] = "Send to eReader"
    message["Date"] = formatdate(localtime=True)
    message["Message-ID"] = make_msgid(domain=mail_from.partition("@")[2] or "calibre-web.com")
    message.set_content("This e-mail has been sent via Calibre-Web.".encode("UTF-8"), "text", "plain")
    if attachment:
        content_type, encoding = mimetypes.guess_type(attachment)
        if content_type is None or encoding is not None:
            content_type = "application/octet-stream"
        main_type, sub_type = content_type.split("/", 1)
        message.add_attachment(data, maintype=main_type, subtype=sub_type, filename=attachment)
    return message


def send(host: str, port: int, login: str, password: str, mail_from: str, recipient: str, message: EmailMessage) -> None:
    # Mirrors TaskEmail.send_standard_email with encryption "None" (mail_use_ssl == 0)
    smtp = smtplib.SMTP(host, port, timeout=600)
    if password:
        smtp.login(str(login), str(password))
    fp = StringIO()
    Generator(fp, mangle_from_=False).flatten(message)
    smtp.sendmail(mail_from, recipient, fp.getvalue())
    smtp.quit()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=2525)
    parser.add_argument("--login", default="cwa")
    parser.add_argument("--password", default="")
    parser.add_argument("--from", dest="mail_from", default="cwa@localhost")
    parser.add_argument("--to", default="kindle@paperroute.local")
    parser.add_argument("--attachment", help="file name to send; omit for a test e-mail")
    parser.add_argument("--bytes", type=int, default=2048)
    args = parser.parse_args()

    message = build_message(args.mail_from, args.to, args.attachment, sample_book(args.attachment or "", args.bytes))
    try:
        send(args.host, args.port, args.login, args.password, args.mail_from, args.to, message)
    except smtplib.SMTPException as e:
        # Same text extraction as TaskEmail.run
        text = e.smtp_error.decode("utf-8").replace("\n", ". ") if hasattr(e, "smtp_error") else str(e)
        print(f"Smtplib Error sending e-mail: {text}")
        return 2
    except socket.error as e:
        print(f"Socket Error sending e-mail: {e.strerror}")
        return 3
    print("E-mail sent")
    return 0


if __name__ == "__main__":
    sys.exit(main())
