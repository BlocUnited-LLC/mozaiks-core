<#import "template.ftl" as layout>
<@layout.registrationLayout displayMessage=!messagesPerField.existsError('firstName','lastName','email','username','password','password-confirm'); section>
    <#if section = "header">
    <#elseif section = "form">
        <div class="mz-login-widget">
            <!-- Brand inside card -->
            <div class="mz-brand">
                <img src="${url.resourcesPath}/img/logo.svg" alt="Mozaiks" class="mz-logo" onerror="this.style.display='none'" />
                <span class="mz-brand-name">Mozaiks</span>
            </div>
            
            <h1 class="mz-title">Create Account</h1>
            
            <#if messagesPerField.existsError('firstName','lastName','email','username','password','password-confirm')>
                <div class="mz-error">
                    <#if messagesPerField.existsError('firstName')>
                        ${kcSanitize(messagesPerField.get('firstName'))?no_esc}<br/>
                    </#if>
                    <#if messagesPerField.existsError('lastName')>
                        ${kcSanitize(messagesPerField.get('lastName'))?no_esc}<br/>
                    </#if>
                    <#if messagesPerField.existsError('email')>
                        ${kcSanitize(messagesPerField.get('email'))?no_esc}<br/>
                    </#if>
                    <#if messagesPerField.existsError('username')>
                        ${kcSanitize(messagesPerField.get('username'))?no_esc}<br/>
                    </#if>
                    <#if messagesPerField.existsError('password')>
                        ${kcSanitize(messagesPerField.get('password'))?no_esc}<br/>
                    </#if>
                    <#if messagesPerField.existsError('password-confirm')>
                        ${kcSanitize(messagesPerField.get('password-confirm'))?no_esc}
                    </#if>
                </div>
            </#if>
            
            <form id="kc-register-form" action="${url.registrationAction}" method="post">
                <div class="mz-input-row">
                    <div class="mz-input-group mz-half">
                        <span class="mz-input-icon">
                            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
                                <circle cx="12" cy="7" r="4"></circle>
                            </svg>
                        </span>
                        <input tabindex="1" id="firstName" name="firstName" type="text" 
                               placeholder="First Name" 
                               value="${(register.formData.firstName!'')}" 
                               aria-invalid="<#if messagesPerField.existsError('firstName')>true</#if>" />
                    </div>
                    
                    <div class="mz-input-group mz-half">
                        <span class="mz-input-icon">
                            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
                                <circle cx="12" cy="7" r="4"></circle>
                            </svg>
                        </span>
                        <input tabindex="2" id="lastName" name="lastName" type="text" 
                               placeholder="Last Name" 
                               value="${(register.formData.lastName!'')}" 
                               aria-invalid="<#if messagesPerField.existsError('lastName')>true</#if>" />
                    </div>
                </div>
                
                <div class="mz-input-group">
                    <span class="mz-input-icon">
                        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"></path>
                            <polyline points="22,6 12,13 2,6"></polyline>
                        </svg>
                    </span>
                    <input tabindex="3" id="email" name="email" type="email" 
                           placeholder="Email" 
                           value="${(register.formData.email!'')}" 
                           autocomplete="email"
                           aria-invalid="<#if messagesPerField.existsError('email')>true</#if>" />
                </div>
                
                <#if !realm.registrationEmailAsUsername>
                    <div class="mz-input-group">
                        <span class="mz-input-icon">
                            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
                                <circle cx="12" cy="7" r="4"></circle>
                            </svg>
                        </span>
                        <input tabindex="4" id="username" name="username" type="text" 
                               placeholder="Username" 
                               value="${(register.formData.username!'')}" 
                               autocomplete="username"
                               aria-invalid="<#if messagesPerField.existsError('username')>true</#if>" />
                    </div>
                </#if>
                
                <div class="mz-input-group">
                    <span class="mz-input-icon">
                        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
                            <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
                        </svg>
                    </span>
                    <input tabindex="5" id="password" name="password" type="password" 
                           placeholder="Password" 
                           autocomplete="new-password"
                           aria-invalid="<#if messagesPerField.existsError('password')>true</#if>" />
                </div>
                
                <div class="mz-input-group">
                    <span class="mz-input-icon">
                        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
                            <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
                        </svg>
                    </span>
                    <input tabindex="6" id="password-confirm" name="password-confirm" type="password" 
                           placeholder="Confirm Password" 
                           autocomplete="new-password"
                           aria-invalid="<#if messagesPerField.existsError('password-confirm')>true</#if>" />
                </div>
                
                <#if recaptchaRequired??>
                    <div class="mz-recaptcha">
                        <div class="g-recaptcha" data-size="compact" data-sitekey="${recaptchaSiteKey}"></div>
                    </div>
                </#if>
                
                <button tabindex="7" type="submit" class="mz-submit">
                    Create Account
                </button>
            </form>
            
            <#if realm.password && social.providers??>
                <div class="mz-divider">
                    <span>or sign up with</span>
                </div>
                
                <div class="mz-social-providers">
                    <#list social.providers as p>
                        <a id="social-${p.alias}" href="${p.loginUrl}" class="mz-social-btn mz-social-${p.alias}" title="${p.displayName}">
                            <#if p.alias == "google">
                                <svg viewBox="0 0 24 24" width="24" height="24">
                                    <path fill="#4285F4" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"/>
                                    <path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"/>
                                    <path fill="#FBBC05" d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z"/>
                                    <path fill="#EA4335" d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"/>
                                </svg>
                            <#elseif p.alias == "github">
                                <svg viewBox="0 0 24 24" width="24" height="24">
                                    <path fill="#fff" d="M12 0c-6.626 0-12 5.373-12 12 0 5.302 3.438 9.8 8.207 11.387.599.111.793-.261.793-.577v-2.234c-3.338.726-4.033-1.416-4.033-1.416-.546-1.387-1.333-1.756-1.333-1.756-1.089-.745.083-.729.083-.729 1.205.084 1.839 1.237 1.839 1.237 1.07 1.834 2.807 1.304 3.492.997.107-.775.418-1.305.762-1.604-2.665-.305-5.467-1.334-5.467-5.931 0-1.311.469-2.381 1.236-3.221-.124-.303-.535-1.524.117-3.176 0 0 1.008-.322 3.301 1.23.957-.266 1.983-.399 3.003-.404 1.02.005 2.047.138 3.006.404 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.874.118 3.176.77.84 1.235 1.911 1.235 3.221 0 4.609-2.807 5.624-5.479 5.921.43.372.823 1.102.823 2.222v3.293c0 .319.192.694.801.576 4.765-1.589 8.199-6.086 8.199-11.386 0-6.627-5.373-12-12-12z"/>
                                </svg>
                            <#elseif p.alias == "microsoft">
                                <svg viewBox="0 0 24 24" width="24" height="24">
                                    <path fill="#F25022" d="M1 1h10v10H1z"/>
                                    <path fill="#00A4EF" d="M1 13h10v10H1z"/>
                                    <path fill="#7FBA00" d="M13 1h10v10H13z"/>
                                    <path fill="#FFB900" d="M13 13h10v10H13z"/>
                                </svg>
                            <#else>
                                <span class="mz-social-icon">${p.displayName[0..0]}</span>
                            </#if>
                        </a>
                    </#list>
                </div>
            </#if>
            
            <div class="mz-register">
                Already have an account? <a href="${url.loginUrl}">Sign In</a>
            </div>
        </div>

        <script>
            (function setFavicon() {
                try {
                    const href = "${url.resourcesPath}/img/logo.svg?v=11";
                    let link = document.querySelector("link[rel='icon']");
                    if (!link) {
                        link = document.createElement('link');
                        link.rel = 'icon';
                        document.head.appendChild(link);
                    }
                    link.type = 'image/svg+xml';
                    link.href = href;
                } catch (e) {
                    // no-op
                }
            })();
        </script>
    </#if>
</@layout.registrationLayout>
