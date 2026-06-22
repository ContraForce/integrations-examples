// Script Include — ContraForceWebhookVerifier
//
// Verifies the HMAC-SHA256 signature ContraForce sends with each webhook.
// Create this as a Script Include (Name: ContraForceWebhookVerifier,
// Accessible from: All application scopes if your Scripted REST API lives in a
// different scope) and call it from the Scripted REST API resource script.
//
// Signature format ContraForce sends:
//   X-CF-Signature: sha256=<lowercase hex of HMAC_SHA256(secret, "{timestamp}.{raw_body}")>
//   X-CF-Timestamp: <unix seconds, also part of the signed message>
//
// Note: GlideCertificateEncryption.generateMac() returns the MAC base64-encoded,
// while ContraForce sends it as lowercase hex, so this helper normalizes the
// computed MAC to hex before comparing. In a *scoped* application the class is
// named CertificateEncryption (new CertificateEncryption()); in global scope it
// is GlideCertificateEncryption, used below.

var ContraForceWebhookVerifier = Class.create();
ContraForceWebhookVerifier.prototype = {
    initialize: function (secret, maxSkewSeconds) {
        this.secret = secret;
        this.maxSkewSeconds = maxSkewSeconds || 300;
    },

    /**
     * @param {string} signatureHeader - value of X-CF-Signature
     * @param {string} timestampHeader - value of X-CF-Timestamp
     * @param {string} rawBody         - the exact request body string
     * @returns {boolean} true when the signature is valid and within clock skew
     */
    verify: function (signatureHeader, timestampHeader, rawBody) {
        if (!signatureHeader || !timestampHeader) {
            return false;
        }

        var ts = parseInt(timestampHeader, 10);
        if (isNaN(ts)) {
            return false;
        }

        // Reject replays / wildly skewed clocks.
        var nowSec = Math.floor(new GlideDateTime().getNumericValue() / 1000);
        if (Math.abs(nowSec - ts) > this.maxSkewSeconds) {
            return false;
        }

        // ContraForce signs the exact bytes "{timestamp}.{raw_body}".
        var message = ts + '.' + rawBody;

        var mac = new GlideCertificateEncryption();
        var expectedHex = this._base64ToHex(mac.generateMac(this.secret, 'HmacSHA256', message));
        var receivedHex = ('' + signatureHeader).replace(/^sha256=/, '').toLowerCase();

        return this._constantTimeEquals(expectedHex, receivedHex);
    },

    _base64ToHex: function (b64) {
        // GlideStringUtil.base64DecodeAsBytes returns a (Java) byte array.
        var bytes = GlideStringUtil.base64DecodeAsBytes(b64);
        var hex = '';
        for (var i = 0; i < bytes.length; i++) {
            var b = bytes[i] & 0xff;
            hex += (b < 16 ? '0' : '') + b.toString(16);
        }
        return hex;
    },

    _constantTimeEquals: function (a, b) {
        if (a.length !== b.length) {
            return false;
        }
        var result = 0;
        for (var i = 0; i < a.length; i++) {
            result |= a.charCodeAt(i) ^ b.charCodeAt(i);
        }
        return result === 0;
    },

    type: 'ContraForceWebhookVerifier'
};
